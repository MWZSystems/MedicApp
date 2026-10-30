using System.Data;
using MedicApp.Data;
using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trainify.Data;
namespace MedicApp.Services;

public class MedicalService(MedicDbContext db, TrainifyDbContext shared, MedicalAccess access, MedicalClock clock, IOptions<MedicalOptions> options)
{
    public void Stamp(MedicalRecord record)
    {
        record.UpdatedById = access.UserId;
        record.LastUpdated = clock.UtcNow;
    }
    public void Audit(string entity, int id, string operation, string reason = "")
    {
        var audit = new AuditEntry { EntityType = entity, EntityId = id, Operation = operation, Reason = reason };
        Stamp(audit);
        db.Audit.Add(audit);
    }
    public void SetVersion(MedicalRecord entity, string version)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(version); }
        catch (FormatException) { throw new MedicalRuleException("Concurrency"); }
        if (bytes.Length != 8) { throw new MedicalRuleException("Concurrency"); }
        db.Entry(entity).Property(x => x.RowVersion).OriginalValue = bytes;
    }
    public async Task<int> BookAsync(int windowId, DateTime localStart, Guid requestKey, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await MedicalLocks.AcquireAsync(db, "MedicApp.Appointments", ct);
        var existing = await db.Appointments.SingleOrDefaultAsync(x => x.RequestKey == requestKey, ct);
        if (existing != null)
        {
            if (existing.PersonId != access.PersonId) { throw new MedicalRuleException("Forbidden"); }
            return existing.Id;
        }
        var window = await db.Windows.SingleOrDefaultAsync(x => x.Id == windowId && x.Available, ct) ?? throw new MedicalRuleException("UnavailableSlot");
        if (!await db.Doctors.AnyAsync(x => x.Id == window.DoctorId && x.Available, ct)) { throw new MedicalRuleException("UnavailableSlot"); }
        var start = clock.ToUtc(localStart);
        var end = start.AddMinutes(window.SlotMinutes);
        MedicalRules.ValidateBooking(start, end, clock.UtcNow, options.Value.BookingHorizonDays, window);
        var personId = access.PersonId;
        if (await db.Appointments.AnyAsync(x => x.Available && x.Status == AppointmentStatus.Scheduled && (x.DoctorId == window.DoctorId || x.PersonId == personId) && x.StartsAt < end && x.EndsAt > start, ct))
        { throw new MedicalRuleException("Overlap"); }
        var appointment = new Appointment { PersonId = personId, DoctorId = window.DoctorId, AvailabilityWindowId = window.Id, StartsAt = start, EndsAt = end, RequestKey = requestKey };
        Stamp(appointment);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(ct);
        Audit("Appointment", appointment.Id, "Book");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return appointment.Id;
    }
    public async Task ChangeAppointmentAsync(int id, AppointmentStatus status, string reason, string version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var item = await db.Appointments.SingleOrDefaultAsync(x => x.Id == id && x.Available, ct) ?? throw new MedicalRuleException("NotFound");
        var doctorId = await access.DoctorIdAsync(ct);
        var isDoctor = doctorId == item.DoctorId;
        if (!isDoctor && item.PersonId != access.PersonId) { throw new MedicalRuleException("Forbidden"); }
        MedicalRules.ValidateAppointmentTransition(item.Status, status, isDoctor, item.StartsAt, clock.UtcNow);
        if (status == AppointmentStatus.Cancelled && string.IsNullOrWhiteSpace(reason)) { throw new MedicalRuleException("ReasonRequired"); }
        SetVersion(item, version);
        item.Status = status;
        item.CancellationReason = status == AppointmentStatus.Cancelled ? reason.Trim() : null;
        Stamp(item);
        Audit("Appointment", id, status.ToString(), reason);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task<int> CreateLeaveAsync(int personId, DateTime localStart, DateTime localEnd, string reference, int? appointmentId, CancellationToken ct)
    {
        var doctor = await access.DoctorIdAsync(ct) ?? throw new MedicalRuleException("Forbidden");
        if (personId == access.PersonId) { throw new MedicalRuleException("Forbidden"); }
        if (!await shared.People.AsNoTracking().AnyAsync(x => x.Id == personId && x.Active, ct)) { throw new MedicalRuleException("NotFound"); }
        var start = clock.ToUtc(localStart);
        var end = clock.ToUtc(localEnd);
        MedicalRules.ValidateInterval(start, end);
        if (appointmentId != null && !await db.Appointments.AnyAsync(x => x.Id == appointmentId && x.PersonId == personId && x.DoctorId == doctor && x.Available && x.Status == AppointmentStatus.Attended, ct))
        { throw new MedicalRuleException("InvalidAppointment"); }
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var leave = new MedicalLeave { PersonId = personId, DoctorId = doctor, StartsAt = start, EndsAt = end, Reference = reference.Trim(), AppointmentId = appointmentId };
        Stamp(leave);
        db.Leaves.Add(leave);
        await db.SaveChangesAsync(ct);
        Audit("MedicalLeave", leave.Id, "Draft");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return leave.Id;
    }
    public async Task ChangeLeaveAsync(int id, LeaveStatus status, string reason, string version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await MedicalLocks.AcquireAsync(db, "MedicApp.LaborEvents", ct);
        var doctor = await access.DoctorIdAsync(ct) ?? throw new MedicalRuleException("Forbidden");
        var leave = await db.Leaves.SingleOrDefaultAsync(x => x.Id == id && x.DoctorId == doctor && x.Available, ct) ?? throw new MedicalRuleException("NotFound");
        MedicalRules.ValidateLeaveTransition(leave.Status, status);
        if (string.IsNullOrWhiteSpace(reason)) { throw new MedicalRuleException("ReasonRequired"); }
        if (status == LeaveStatus.Approved && await db.Leaves.AnyAsync(x => x.Id != id && x.PersonId == leave.PersonId && x.Available && x.Status == LeaveStatus.Approved && x.StartsAt < leave.EndsAt && x.EndsAt > leave.StartsAt, ct))
        { throw new MedicalRuleException("Overlap"); }
        SetVersion(leave, version);
        leave.Status = status;
        leave.Revision++;
        leave.CancellationReason = status == LeaveStatus.Cancelled ? reason.Trim() : null;
        Stamp(leave);
        var labor = new LaborEvent { MedicalLeaveId = id, PersonId = leave.PersonId, StartsAt = leave.StartsAt, EndsAt = leave.EndsAt, Status = status, Revision = leave.Revision };
        Stamp(labor);
        db.LaborEvents.Add(labor);
        Audit("MedicalLeave", id, status.ToString(), reason);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task AddWindowAsync(DateTime localStart, DateTime localEnd, int minutes, CancellationToken ct)
    {
        var doctor = await access.DoctorIdAsync(ct) ?? throw new MedicalRuleException("Forbidden");
        var start = clock.ToUtc(localStart);
        var end = clock.ToUtc(localEnd);
        MedicalRules.ValidateInterval(start, end);
        if (start <= clock.UtcNow || minutes < 5 || minutes > 120 || (end - start).TotalMinutes % minutes != 0) { throw new MedicalRuleException("UnavailableSlot"); }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.Windows.AnyAsync(x => x.DoctorId == doctor && x.Available && x.StartsAt < end && x.EndsAt > start, ct)) { throw new MedicalRuleException("Overlap"); }
        var window = new AvailabilityWindow { DoctorId = doctor, StartsAt = start, EndsAt = end, SlotMinutes = minutes };
        Stamp(window);
        db.Windows.Add(window);
        await db.SaveChangesAsync(ct);
        Audit("AvailabilityWindow", window.Id, "Create");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
