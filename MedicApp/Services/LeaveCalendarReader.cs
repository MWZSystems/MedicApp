using System.Globalization;
using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Trainify.Data;
namespace MedicApp.Services;

public class LeaveCalendarReader(MedicDbContext db, TrainifyDbContext shared, MedicalAccess access, MedicalClock clock, IStringLocalizer<UiText> text)
{
    public async Task<bool> CanSearchPeopleAsync(CancellationToken ct) => await access.DoctorIdAsync(ct) != null || await access.CanManageDoctorsAsync(ct) || await access.CanManageAccessAsync(ct);
    public async Task<IReadOnlyList<LeaveCalendarPerson>> SearchAsync(string? term, CancellationToken ct)
    {
        term = term == null ? null : string.Join(' ', term.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (term == null || term.Length < 2 || term.Length > 80) { return []; }
        var canSearch = await CanSearchPeopleAsync(ct);
        var query = shared.People.AsNoTracking().AsQueryable();
        if (!canSearch) { var own = access.PersonId; query = query.Where(x => x.Id == own); }
        // Include inactive people so authorized staff can review historical leave periods.
        return await query.Where(x => x.EmployeeNumber.Contains(term) || (x.FirstName + " " + (x.LastName ?? "")).Contains(term))
            .OrderBy(x => x.EmployeeNumber).ThenBy(x => x.Id).Take(20)
            .Select(x => new LeaveCalendarPerson(x.Id, x.FirstName + " " + (x.LastName ?? ""), x.EmployeeNumber)).ToListAsync(ct);
    }
    private async Task<LeaveCalendarPerson> PersonAsync(int id, bool canSearch, CancellationToken ct)
    {
        if (!canSearch && id != access.PersonId) { throw new MedicalRuleException("Forbidden"); }
        return await shared.People.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new LeaveCalendarPerson(x.Id, x.FirstName + " " + (x.LastName ?? ""), x.EmployeeNumber)).SingleOrDefaultAsync(ct) ?? throw new MedicalRuleException("NotFound");
    }
    public async Task<LeaveCalendarPage> PageAsync(int? personId, CancellationToken ct)
    {
        var canSearch = await CanSearchPeopleAsync(ct);
        if (!canSearch && personId == null) { personId = access.PersonId; }
        var today = DateOnly.FromDateTime(clock.Local(clock.UtcNow));
        if (personId == null) { return new() { CanSearchPeople = canSearch, FocusDate = today }; }
        var person = await PersonAsync(personId.Value, canSearch, ct);
        var query = db.Leaves.AsNoTracking().Where(x => x.PersonId == person.Id && x.Available && x.Status == LeaveStatus.Approved);
        var now = clock.UtcNow;
        var date = today;
        if (!await query.AnyAsync(x => x.StartsAt <= now && x.EndsAt > now, ct))
        {
            var next = await query.Where(x => x.StartsAt > now).OrderBy(x => x.StartsAt).Select(x => (DateTime?)x.StartsAt).FirstOrDefaultAsync(ct);
            var latest = next ?? await query.Where(x => x.EndsAt <= now).OrderByDescending(x => x.EndsAt).ThenByDescending(x => x.Id).Select(x => (DateTime?)x.StartsAt).FirstOrDefaultAsync(ct);
            if (latest != null) { date = DateOnly.FromDateTime(clock.Local(latest.Value)); }
        }
        return new() { CanSearchPeople = canSearch, Person = person, FocusDate = date };
    }
    public async Task<IReadOnlyList<LeaveCalendarEvent>> EventsAsync(int personId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        CalendarReader.ValidateRange(from, to);
        await PersonAsync(personId, await CanSearchPeopleAsync(ct), ct);
        var start = clock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var end = clock.ToUtc(to.ToDateTime(TimeOnly.MinValue));
        var rows = await db.Leaves.AsNoTracking().Where(x => x.PersonId == personId && x.Available && x.Status == LeaveStatus.Approved && x.StartsAt < end && x.EndsAt > start)
            .OrderBy(x => x.StartsAt).ThenBy(x => x.Id).Select(x => new { x.Id, x.StartsAt, x.EndsAt }).Take(5001).ToListAsync(ct);
        if (rows.Count > 5000) { throw new MedicalRuleException("InvalidRange"); }
        return rows.Select(row =>
        {
            var localStart = clock.Local(row.StartsAt);
            var localEnd = clock.Local(row.EndsAt);
            var firstDay = DateOnly.FromDateTime(localStart);
            var exclusiveEnd = DateOnly.FromDateTime(localEnd);
            if (TimeOnly.FromDateTime(localEnd) != TimeOnly.MinValue) { exclusiveEnd = exclusiveEnd.AddDays(1); }
            return new LeaveCalendarEvent(row.Id.ToString(CultureInfo.InvariantCulture), text["ApprovedLeaveCalendar"].Value,
                firstDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), exclusiveEnd.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), true, "#147d91",
                new LeaveCalendarDetails(localStart.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture), localEnd.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture), exclusiveEnd.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }).ToList();
    }
}
