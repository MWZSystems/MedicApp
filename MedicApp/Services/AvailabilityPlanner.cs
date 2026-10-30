using MedicApp.Data;
using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trainify.Data;
namespace MedicApp.Services;

public record BookingWindow(int Id, int DoctorId, int PersonId, DateTime StartsAt, DateTime EndsAt, int SlotMinutes);
public class AvailabilityPlanner(MedicDbContext db, TrainifyDbContext shared, MedicalClock clock, IOptions<MedicalOptions> options)
{
    public async Task<IReadOnlyList<BookingWindow>> WindowsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        CalendarReader.ValidateRange(from, to);
        var start = clock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var end = clock.ToUtc(to.ToDateTime(TimeOnly.MinValue));
        var doctors = await db.Doctors.AsNoTracking().Where(x => x.Available && (!options.Value.DemoBookingEnabled || x.ProfessionalLicense == BookingSetup.DemoLicense)).OrderBy(x => x.Id).Take(101).ToListAsync(ct);
        if (doctors.Count > 100) { throw new MedicalRuleException("InvalidRange"); }
        var personIds = doctors.Select(x => x.PersonId).ToArray();
        var people = await shared.People.AsNoTracking().Where(x => x.Active && personIds.Contains(x.Id)).Select(x => new { x.Id, Demo = x.EmployeeNumber.StartsWith(DemoDataService.Prefix) }).ToListAsync(ct);
        var permitted = people.Where(x => options.Value.DemoBookingEnabled ? x.Demo : !x.Demo).Select(x => x.Id).ToHashSet();
        doctors = doctors.Where(x => permitted.Contains(x.PersonId)).ToList();
        var manual = doctors.Where(x => !x.UseRecurringSchedule).Select(x => x.Id).ToArray();
        var rows = await db.Windows.AsNoTracking().Where(x => x.Available && !x.Generated && manual.Contains(x.DoctorId) && x.SlotMinutes <= 30 && x.StartsAt < end && x.EndsAt > start).OrderBy(x => x.Id).Take(2001).ToListAsync(ct);
        if (rows.Count > 2000) { throw new MedicalRuleException("InvalidRange"); }
        var names = doctors.ToDictionary(x => x.Id, x => x.PersonId);
        var result = rows.Select(x => new BookingWindow(x.Id, x.DoctorId, names[x.DoctorId], x.StartsAt, x.EndsAt, x.SlotMinutes)).ToList();
        foreach (var doctor in doctors.Where(x => x.UseRecurringSchedule))
        {
            for (var day = from; day < to; day = day.AddDays(1))
            {
                result.Add(new BookingWindow(0, doctor.Id, doctor.PersonId, clock.ToUtc(day.ToDateTime(TimeOnly.MinValue)), clock.ToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue)), options.Value.DefaultSlotMinutes));
            }
        }
        return result;
    }
    public async Task<int> MaterializeAsync(BookingWindow window, string actor, CancellationToken ct)
    {
        if (window.Id != 0) { return window.Id; }
        var row = await db.Windows.SingleOrDefaultAsync(x => x.Generated && x.Available && x.DoctorId == window.DoctorId && x.StartsAt == window.StartsAt && x.EndsAt == window.EndsAt && x.SlotMinutes == window.SlotMinutes, ct);
        if (row != null) { return row.Id; }
        row = new AvailabilityWindow { Generated = true, DoctorId = window.DoctorId, StartsAt = window.StartsAt, EndsAt = window.EndsAt, SlotMinutes = window.SlotMinutes, UpdatedById = actor, LastUpdated = clock.UtcNow };
        db.Windows.Add(row);
        await db.SaveChangesAsync(ct);
        db.Audit.Add(new AuditEntry { EntityType = "AvailabilityWindow", EntityId = row.Id, Operation = "GenerateRecurring", UpdatedById = actor, LastUpdated = clock.UtcNow });
        return row.Id;
    }
}
