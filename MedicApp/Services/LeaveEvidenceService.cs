using System.Data;
using MedicApp.Data;
using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace MedicApp.Services;

public class LeaveEvidenceService(MedicDbContext db, LeaveRecordAccess records, MedicalAccess access, MedicalService medical, EvidenceStorage storage, IOptions<DocumentOptions> options)
{
    public async Task UploadAsync(int leaveId, Guid requestKey, IReadOnlyList<IFormFile> files, CancellationToken ct)
    {
        if (requestKey == Guid.Empty) { throw new MedicalRuleException("InvalidReservation"); }
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await MedicalLocks.AcquireAsync(db, "MedicApp.LeaveEvidence." + leaveId.ToString(System.Globalization.CultureInfo.InvariantCulture), ct);
        await records.GetAsync(leaveId, true, ct);
        var previous = await db.LeaveDocuments.AsNoTracking().Where(x => x.MedicalLeaveId == leaveId && x.RequestKey == requestKey).Select(x => x.UpdatedById).ToListAsync(ct);
        if (previous.Count != 0) { if (previous.Any(x => x != access.UserId)) { throw new MedicalRuleException("Forbidden"); } return; }
        if (await db.LeaveDocuments.CountAsync(x => x.MedicalLeaveId == leaveId, ct) + files.Count > options.Value.MaxDocumentsPerLeave) { throw new MedicalRuleException("EvidenceLimits"); }
        var prepared = await storage.PrepareAsync(files, ct);
        var written = new List<string>(); var documents = new List<LeaveDocument>();
        try
        {
            foreach (var file in prepared)
            {
                var key = Guid.NewGuid().ToString("N"); await storage.WriteAsync(key, file.Bytes, ct); written.Add(key);
                var document = new LeaveDocument { MedicalLeaveId = leaveId, StorageKey = key, OriginalName = file.OriginalName, ContentType = file.ContentType, ByteCount = file.Bytes.LongLength, Sha256 = file.Sha256, RequestKey = requestKey, Ordinal = documents.Count };
                medical.Stamp(document); documents.Add(document);
            }
            db.LeaveDocuments.AddRange(documents); await db.SaveChangesAsync(ct);
            foreach (var document in documents) { medical.Audit("LeaveDocument", document.Id, "Upload"); }
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        catch { foreach (var key in written) { storage.Remove(key); } throw; }
    }
    public async Task<(LeaveDocument Document, byte[] Bytes)> DownloadAsync(int id, CancellationToken ct)
    {
        var document = await db.LeaveDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Available, ct) ?? throw new MedicalRuleException("NotFound");
        await records.GetAsync(document.MedicalLeaveId, false, ct);
        var bytes = await storage.ReadAsync(document, ct);
        medical.Audit("LeaveDocument", id, "Download"); await db.SaveChangesAsync(ct);
        return (document, bytes);
    }
}
