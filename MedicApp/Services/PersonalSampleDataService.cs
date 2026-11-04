using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MedicApp.Data;
using MedicApp.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Trainify.Data;
namespace MedicApp.Services;

public record PersonalSampleResult(bool Created, string Batch, int Appointments, int Leaves);

/// <summary>Explicit, audited visual samples for one existing account; never creates labor events.</summary>
public sealed class PersonalSampleDataService(IConfiguration configuration, MedicalClock clock)
{
    public const string Prefix = "MEDIC-DEMO-VISUAL-";
    private static readonly int[] Days = [1, 2, 5, 12, 20, 27];
    public static bool IsSample(string reference) => reference.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    private static Guid Key(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
    public async Task<PersonalSampleResult> CreateAsync(string userName, string employeeNumber, string actorName, DateOnly firstMonth, CancellationToken ct)
    {
        if (firstMonth.Day != 1 || firstMonth.Year < 2000 || firstMonth.Year > 2099 || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(employeeNumber)) { throw new MedicalRuleException("InvalidRange"); }
        await using var connection = new SqlConnection(DatabaseConfiguration.Resolve(configuration));
        await using var db = new MedicDbContext(new DbContextOptionsBuilder<MedicDbContext>().UseSqlServer(connection).Options);
        await using var shared = new TrainifyDbContext(new DbContextOptionsBuilder<TrainifyDbContext>().UseSqlServer(connection).Options);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await shared.Database.UseTransactionAsync(tx.GetDbTransaction(), ct);
        await MedicalLocks.AcquireAsync(db, "MedicApp.PersonalVisualSamples", ct);
        var actor = await shared.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserName == actorName && x.Active && x.Person.Active, ct) ?? throw new MedicalRuleException("Forbidden");
        if (!await db.AccessProfiles.AnyAsync(x => x.UserId == actor.Id && x.Available && x.ManageAccess, ct)) { throw new MedicalRuleException("Forbidden"); }
        var user = await shared.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserName == userName && x.Active && x.Person.Active && x.Person.EmployeeNumber == employeeNumber, ct) ?? throw new MedicalRuleException("NotFound");
        var doctor = await db.Doctors.AsNoTracking().SingleOrDefaultAsync(x => x.ProfessionalLicense == DemoDataService.DoctorLicense, ct) ?? throw new MedicalRuleException("VisualSampleDoctorMissing");
        if (!await shared.People.AnyAsync(x => x.Id == doctor.PersonId && x.EmployeeNumber == DemoDataService.Prefix + "D1", ct)) { throw new MedicalRuleException("Concurrency"); }
        var batch = Prefix + user.PersonId.ToString(CultureInfo.InvariantCulture) + "-" + firstMonth.ToString("yyyyMM", CultureInfo.InvariantCulture) + "-2M";
        if (await db.Audit.AnyAsync(x => x.EntityType == "VisualSampleBatch" && x.Reason == batch, ct))
        {
            var appointments = await db.Audit.CountAsync(x => x.EntityType == "VisualSampleAppointment" && x.Reason == batch, ct);
            var leaves = await db.Audit.CountAsync(x => x.EntityType == "VisualSampleLeave" && x.Reason == batch, ct);
            return new(false, batch, appointments, leaves);
        }
        var createdAppointments = new List<Appointment>(); var createdLeaves = new List<MedicalLeave>(); var windows = new List<AvailabilityWindow>();
        for (var month = 0; month < 2; month++)
        {
            var first = firstMonth.AddMonths(month);
            for (var index = 0; index < Days.Length; index++)
            {
                var local = first.AddDays(Days[index] - 1).ToDateTime(new TimeOnly(8 + index * 2, 0));
                var start = clock.ToUtc(local);
                // Respect existing appointments: move the sample within the same day instead of changing real records.
                var found = false;
                for (var slot = 0; slot < 12; slot++)
                {
                    var end = start.AddMinutes(30);
                    if (!await db.Appointments.AnyAsync(x => x.Available && (x.PersonId == user.PersonId || x.DoctorId == doctor.Id) && x.StartsAt < end && x.EndsAt > start, ct)) { found = true; break; }
                    start = start.AddMinutes(30);
                    if (DateOnly.FromDateTime(clock.Local(start)) != first.AddDays(Days[index] - 1)) { break; }
                }
                if (!found) { throw new MedicalRuleException("Overlap"); }
                var window = new AvailabilityWindow { DoctorId = doctor.Id, Generated = true, StartsAt = start, EndsAt = start.AddMinutes(30), SlotMinutes = 30, UpdatedById = actor.Id, LastUpdated = clock.UtcNow };
                db.Windows.Add(window); await db.SaveChangesAsync(ct); windows.Add(window);
                var status = index == 4 ? AppointmentStatus.Cancelled : start < clock.UtcNow ? index == 1 ? AppointmentStatus.NoShow : AppointmentStatus.Attended : AppointmentStatus.Scheduled;
                var appointment = new Appointment { PersonId = user.PersonId, DoctorId = doctor.Id, AvailabilityWindowId = window.Id, StartsAt = start, EndsAt = start.AddMinutes(30), Status = status, CancellationReason = status == AppointmentStatus.Cancelled ? "DEMO: cancelación ficticia para visualización." : null, RequestKey = Key(batch + "-APPT-" + month + "-" + index), UpdatedById = actor.Id, LastUpdated = clock.UtcNow };
                db.Appointments.Add(appointment); createdAppointments.Add(appointment);
            }
            for (var index = 0; index < 2; index++)
            {
                var local = first.AddDays(index == 0 ? 8 : 22).ToDateTime(TimeOnly.MinValue);
                var leave = new MedicalLeave { PersonId = user.PersonId, DoctorId = doctor.Id, StartsAt = clock.ToUtc(local), EndsAt = clock.ToUtc(local.AddDays(index == 0 ? 3 : 2)), Status = LeaveStatus.Draft, Reference = batch + "-LEAVE-" + month + "-" + index, UpdatedById = actor.Id, LastUpdated = clock.UtcNow };
                db.Leaves.Add(leave); createdLeaves.Add(leave);
            }
        }
        await db.SaveChangesAsync(ct);
        void Audit(string type, int id) => db.Audit.Add(new AuditEntry { EntityType = type, EntityId = id, Operation = "Create", Reason = batch, UpdatedById = actor.Id, LastUpdated = clock.UtcNow });
        foreach (var row in createdAppointments) { Audit("VisualSampleAppointment", row.Id); }
        foreach (var row in createdLeaves) { Audit("VisualSampleLeave", row.Id); }
        foreach (var row in windows) { Audit("VisualSampleWindow", row.Id); }
        Audit("VisualSampleBatch", user.PersonId);
        await db.SaveChangesAsync(ct);
        if (await db.LaborEvents.AnyAsync(x => createdLeaves.Select(l => l.Id).Contains(x.MedicalLeaveId), ct)) { throw new MedicalRuleException("Concurrency"); }
        await tx.CommitAsync(ct);
        return new(true, batch, createdAppointments.Count, createdLeaves.Count);
    }
}
