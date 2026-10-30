using System.Globalization;
using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Trainify.Data;
namespace MedicApp.Services;

public class CalendarReader(MedicDbContext db, TrainifyDbContext shared, MedicalAccess access, MedicalClock clock, ListReader names, IOptions<MedicalOptions> options, IStringLocalizer<UiText> text)
{
    public static void ValidateRange(DateOnly from, DateOnly to)
    {
        if (to <= from || to.DayNumber - from.DayNumber > 93) { throw new MedicalRuleException("InvalidRange"); }
    }
    public async Task<IReadOnlyList<CalendarSlot>> SlotsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        ValidateRange(from, to);
        var start = clock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var end = clock.ToUtc(to.ToDateTime(TimeOnly.MinValue));
        var now = clock.UtcNow;
        var horizon = now.AddDays(options.Value.BookingHorizonDays);
        if (end <= now || start > horizon) { return []; }
        var windows = await (from w in db.Windows.AsNoTracking()
                             join d in db.Doctors.AsNoTracking() on w.DoctorId equals d.Id
                             where w.Available && d.Available && w.StartsAt < end && w.StartsAt <= horizon && w.EndsAt > start && w.EndsAt > now
                             orderby d.Id, w.Id
                             select new { Window = w, d.PersonId }).Take(2001).ToListAsync(ct);
        if (windows.Count > 2000) { throw new MedicalRuleException("InvalidRange"); }
        var doctorPeople = windows.Select(x => x.PersonId).Distinct().ToArray();
        var activePeople = await shared.People.AsNoTracking().Where(x => doctorPeople.Contains(x.Id) && x.Active).Select(x => x.Id).ToListAsync(ct);
        windows = windows.Where(x => activePeople.Contains(x.PersonId)).ToList();
        var doctorIds = windows.Select(x => x.Window.DoctorId).Distinct().ToArray();
        var busy = await db.Appointments.AsNoTracking().Where(x => x.Available && x.Status == AppointmentStatus.Scheduled && (doctorIds.Contains(x.DoctorId) || x.PersonId == access.PersonId) && x.StartsAt < end && x.EndsAt > start)
            .Select(x => new { x.DoctorId, x.PersonId, x.StartsAt, x.EndsAt }).ToListAsync(ct);
        var result = new SortedDictionary<DateTime, CalendarSlot>();
        foreach (var row in windows)
        {
            var window = row.Window;
            var step = TimeSpan.FromMinutes(window.SlotMinutes);
            var earliest = start > now ? start : now;
            var at = window.StartsAt;
            if (at < earliest)
            {
                var skip = (long)Math.Ceiling((earliest - at).Ticks / (double)step.Ticks);
                at = at.AddTicks(skip * step.Ticks);
            }
            for (; at < end && at <= horizon && at.Add(step) <= window.EndsAt; at = at.Add(step))
            {
                if (at <= now || result.ContainsKey(at)) { continue; }
                if (busy.Any(x => (x.DoctorId == window.DoctorId || x.PersonId == access.PersonId) && MedicalRules.Overlaps(at, at.Add(step), x.StartsAt, x.EndsAt))) { continue; }
                result.Add(at, new CalendarSlot(clock.Local(at), window.SlotMinutes));
                if (result.Count > 10000) { throw new MedicalRuleException("InvalidRange"); }
            }
        }
        return result.Values.ToList();
    }
    public async Task<IReadOnlyList<CalendarEvent>> EventsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        ValidateRange(from, to);
        var start = clock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var end = clock.ToUtc(to.ToDateTime(TimeOnly.MinValue));
        var doctorId = await access.DoctorIdAsync(ct);
        var rows = await db.Appointments.AsNoTracking().Where(x => x.Available && (x.PersonId == access.PersonId || x.DoctorId == doctorId) && x.StartsAt < end && x.EndsAt > start)
            .OrderBy(x => x.StartsAt).ThenBy(x => x.Id).Take(5001)
            .Select(x => new { x.Id, x.PersonId, x.DoctorId, x.StartsAt, x.EndsAt, x.Status, x.RowVersion }).ToListAsync(ct);
        if (rows.Count > 5000) { throw new MedicalRuleException("InvalidRange"); }
        var ids = rows.Select(x => x.DoctorId).Distinct().ToArray();
        var doctors = await db.Doctors.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.PersonId }).ToListAsync(ct);
        var people = await names.NamesAsync(rows.Select(x => x.PersonId).Concat(doctors.Select(x => x.PersonId)), ct);
        var doctorNames = doctors.ToDictionary(x => x.Id, x => people.GetValueOrDefault(x.PersonId, text["UnknownPerson"].Value));
        var result = new List<CalendarEvent>();
        foreach (var row in rows)
        {
            var isDoctor = doctorId == row.DoctorId;
            var employeeName = people.GetValueOrDefault(row.PersonId, text["UnknownPerson"].Value);
            var doctorName = doctorNames.GetValueOrDefault(row.DoctorId, text["UnknownPerson"].Value);
            var title = isDoctor ? employeeName : text["MyAppointment"].Value;
            var state = text[row.Status.ToString()].Value;
            var color = row.Status switch { AppointmentStatus.Attended => "#237b54", AppointmentStatus.Cancelled => "#74818a", AppointmentStatus.NoShow => "#a44f36", _ => "#147d91" };
            result.Add(new(row.Id.ToString(CultureInfo.InvariantCulture), $"{title} · {state}", LocalText(row.StartsAt), LocalText(row.EndsAt), false, color, ["calendar-appointment"],
                new CalendarEventDetails
                {
                    AppointmentId = row.Id,
                    Employee = employeeName,
                    Doctor = doctorName,
                    Status = state,
                    Version = Convert.ToBase64String(row.RowVersion),
                    CanCancel = row.Status == AppointmentStatus.Scheduled && (isDoctor || row.StartsAt > clock.UtcNow),
                    CanAttend = isDoctor && row.Status == AppointmentStatus.Scheduled && row.StartsAt <= clock.UtcNow
                }));
        }
        var slots = await SlotsAsync(from, to, ct);
        foreach (var day in slots.GroupBy(x => DateOnly.FromDateTime(x.StartsAtLocal)))
        {
            var date = day.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            result.Add(new("free-" + date, text["FreeSlots", day.Count()].Value, date, null, true, "#dff2eb", ["calendar-availability"], new CalendarEventDetails { Kind = "availability", Date = date }));
        }
        return result;
    }
    private string LocalText(DateTime value) => clock.Local(value).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
}
