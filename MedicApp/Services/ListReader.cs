using MedicApp.Models.Entities;
using MedicApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Trainify.Data;
namespace MedicApp.Services;

public class ListReader(TrainifyDbContext shared, MedicalClock clock)
{
    public async Task<ListPage<T>> PageAsync<T>(IQueryable<T> query, int page, string? search, DateTime? from, DateTime? to, CancellationToken ct) where T : MedicalRecord
    {
        page = Math.Clamp(page, 1, 100000);
        var total = await query.CountAsync(ct);
        return new ListPage<T> { Items = await query.Skip((page - 1) * 25).Take(25).ToListAsync(ct), Total = total, Page = page, Query = search ?? "", From = from ?? clock.Local(clock.UtcNow).Date.AddDays(-30), To = to ?? clock.Local(clock.UtcNow).Date.AddDays(30) };
    }
    public async Task<Dictionary<int, string>> NamesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var keys = ids.Distinct().ToArray();
        return await shared.People.AsNoTracking().Where(x => keys.Contains(x.Id)).Select(x => new { x.Id, Name = x.EmployeeNumber + " · " + x.FirstName + " " + (x.LastName ?? "") }).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
    }
    public (DateTime Start, DateTime End) Range(DateTime? from, DateTime? to)
    {
        var start = (from ?? clock.Local(clock.UtcNow).Date.AddDays(-30)).Date;
        var end = (to ?? start.AddDays(60)).Date.AddDays(1);
        if (end <= start || (end - start).TotalDays > 93) { throw new MedicalRuleException("InvalidRange"); }
        return (clock.ToUtc(start), clock.ToUtc(end));
    }
}
