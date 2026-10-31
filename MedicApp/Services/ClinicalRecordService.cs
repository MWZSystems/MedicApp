using System.ComponentModel.DataAnnotations;
using System.Data;
using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trainify.Data;
namespace MedicApp.Services;

public class ClinicalRecordService(MedicDbContext db, TrainifyDbContext shared, MedicalAccess access, MedicalService medical, MedicalClock clock, EvidenceStorage storage, IOptions<DocumentOptions> options)
{
    public async Task<LeaveCalendarPerson> PersonAsync(int id, bool writing, CancellationToken ct)
    {
        if (!await access.CanUseClinicalRecordsAsync(ct)) { throw new MedicalRuleException("Forbidden"); }
        var person = await shared.People.AsNoTracking().Where(x => x.Id == id && (!writing || x.Active)).Select(x => new { x.Id, x.FirstName, x.LastName, x.EmployeeNumber }).SingleOrDefaultAsync(ct) ?? throw new MedicalRuleException("NotFound");
        if (writing && person.EmployeeNumber.StartsWith("MEDIC-DEMO-", StringComparison.Ordinal)) { throw new MedicalRuleException("DemoClinicalForbidden"); }
        if (writing && await shared.People.AnyAsync(x => x.Id == access.PersonId && x.EmployeeNumber.StartsWith("MEDIC-DEMO-"), ct)) { throw new MedicalRuleException("DemoClinicalForbidden"); }
        return new(person.Id, person.FirstName + " " + person.LastName, person.EmployeeNumber);
    }
    public async Task<int> AddVisitAsync(ClinicalVisitForm input, CancellationToken ct)
    {
        if (!Validator.TryValidateObject(input, new ValidationContext(input), [], true) || input.RequestKey == Guid.Empty) { throw new MedicalRuleException("InvalidClinicalInput"); }
        await PersonAsync(input.PersonId, true, ct);
        var utc = clock.ToUtc(input.VisitedAt);
        if (utc > clock.UtcNow.AddMinutes(5) || utc < new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)) { throw new MedicalRuleException("InvalidClinicalDate"); }
        if (input.Systolic != null && input.Diastolic != null && input.Systolic <= input.Diastolic) { throw new MedicalRuleException("InvalidBloodPressure"); }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await MedicalLocks.AcquireAsync(db, "MedicApp.ClinicalVisits", ct);
        var existing = await db.ClinicalVisits.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == input.RequestKey, ct);
        if (existing != null)
        {
            if (existing.PersonId != input.PersonId || existing.UpdatedById != access.UserId) { throw new MedicalRuleException("Forbidden"); }
            return existing.Id;
        }
        var visit = new ClinicalVisit
        {
            PersonId = input.PersonId,
            VisitedAt = utc,
            RequestKey = input.RequestKey,
            Reason = input.Reason?.Trim() ?? "",
            Findings = input.Findings?.Trim() ?? "",
            Diagnosis = input.Diagnosis?.Trim() ?? "",
            Treatment = input.Treatment?.Trim() ?? "",
            Prescription = input.Prescription?.Trim() ?? "",
            Notes = input.Notes?.Trim() ?? "",
            Weight = input.Weight,
            Height = input.Height,
            Temperature = input.Temperature,
            Systolic = input.Systolic,
            Diastolic = input.Diastolic,
            Pulse = input.Pulse,
            RespiratoryRate = input.RespiratoryRate,
            OxygenSaturation = input.OxygenSaturation,
            Glucose = input.Glucose
        };
        medical.Stamp(visit); db.ClinicalVisits.Add(visit); await db.SaveChangesAsync(ct);
        medical.Audit("ClinicalVisit", visit.Id, "Create"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return visit.Id;
    }
    public async Task UploadAsync(PatientUploadForm input, CancellationToken ct)
    {
        if (!Validator.TryValidateObject(input, new ValidationContext(input), [], true) || input.RequestKey == Guid.Empty) { throw new MedicalRuleException("InvalidClinicalInput"); }
        await PersonAsync(input.PersonId, true, ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await MedicalLocks.AcquireAsync(db, "MedicApp.PatientEvidence." + input.PersonId.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
        if (input.ClinicalVisitId != null && !await db.ClinicalVisits.AnyAsync(x => x.Id == input.ClinicalVisitId && x.PersonId == input.PersonId && x.Available, ct)) { throw new MedicalRuleException("NotFound"); }
        var previous = await db.PatientDocuments.AsNoTracking().Where(x => x.PersonId == input.PersonId && x.RequestKey == input.RequestKey).Select(x => x.UpdatedById).ToListAsync(ct);
        if (previous.Count != 0) { if (previous.Any(x => x != access.UserId)) { throw new MedicalRuleException("Forbidden"); } return; }
        if (await db.PatientDocuments.CountAsync(x => x.PersonId == input.PersonId, ct) + input.Files.Count > options.Value.MaxDocumentsPerPatient) { throw new MedicalRuleException("EvidenceLimits"); }
        var prepared = await storage.PrepareAsync(input.Files, ct);
        var written = new List<string>(); var documents = new List<PatientDocument>();
        try
        {
            foreach (var file in prepared)
            {
                var key = Guid.NewGuid().ToString("N"); await storage.WriteAsync(key, file.Bytes, ct); written.Add(key);
                var document = new PatientDocument { PersonId = input.PersonId, ClinicalVisitId = input.ClinicalVisitId, Kind = input.Kind, Description = input.Description?.Trim() ?? "", StorageKey = key, OriginalName = file.OriginalName, ContentType = file.ContentType, ByteCount = file.Bytes.LongLength, Sha256 = file.Sha256, RequestKey = input.RequestKey, Ordinal = documents.Count };
                medical.Stamp(document); documents.Add(document);
            }
            db.PatientDocuments.AddRange(documents); await db.SaveChangesAsync(ct);
            foreach (var document in documents) { medical.Audit("PatientDocument", document.Id, "Upload"); }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        catch { foreach (var key in written) { storage.Remove(key); } throw; }
    }
    public async Task<(PatientDocument Document, byte[] Bytes)> DownloadAsync(int id, CancellationToken ct)
    {
        if (!await access.CanUseClinicalRecordsAsync(ct)) { throw new MedicalRuleException("Forbidden"); }
        var document = await db.PatientDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Available, ct) ?? throw new MedicalRuleException("NotFound");
        await PersonAsync(document.PersonId, false, ct);
        var bytes = await storage.ReadAsync(document.StorageKey, document.ByteCount, document.Sha256, ct);
        medical.Audit("PatientDocument", id, "Download"); await db.SaveChangesAsync(ct); return (document, bytes);
    }
}
