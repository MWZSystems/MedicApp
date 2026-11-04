using MedicApp.Data;
using MedicApp.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
namespace MedicApp.Services;

public record ListFilter(string Key, string Label, IReadOnlyList<SelectListItem>? Options = null, bool Date = false, string? DefaultValue = null);

/// <summary>Read-only composable identity lookups on the same context: no schema or tracked model changes.</summary>
public static class ListFiltering
{
    public sealed class PersonLookup { public int Id { get; set; } public string EmployeeNumber { get; set; } = ""; public string FirstName { get; set; } = ""; public string? LastName { get; set; } public string? SecondLastName { get; set; } }
    public sealed class AccountLookup { public string Id { get; set; } = ""; public int PersonId { get; set; } public string? UserName { get; set; } }
    public static IQueryable<int> People(MedicDbContext db, string search) => db.Database.SqlQuery<PersonLookup>($"SELECT Id, EmployeeNumber, FirstName, LastName, SecondLastName FROM dbo.SY_People")
        .Where(x => x.EmployeeNumber.Contains(search) || (x.FirstName + " " + (x.LastName ?? "") + " " + (x.SecondLastName ?? "")).Contains(search)).Select(x => x.Id);
    public static IQueryable<string> Accounts(MedicDbContext db, string search)
    {
        var people = People(db, search);
        return db.Database.SqlQuery<AccountLookup>($"SELECT Id, PersonId, UserName FROM dbo.SC_Users")
            .Where(x => (x.UserName != null && x.UserName.Contains(search)) || people.Contains(x.PersonId)).Select(x => x.Id);
    }
    // Detail routes need explicit route values; changing action invalidates ambient IDs in MVC URL generation.
    public static IActionResult RedirectBack(this Controller controller, string action, object routeValues)
    {
        var referer = controller.Request.GetTypedHeaders().Referer;
        var target = controller.Url.Action(action, routeValues);
        if (referer is { IsAbsoluteUri: true } && target != null && referer.Authority == controller.Request.Host.Value
            && string.Equals(referer.AbsolutePath.TrimEnd('/'), target.Split('?')[0].TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            && controller.Url.IsLocalUrl(referer.PathAndQuery))
        {
            return controller.LocalRedirect(referer.PathAndQuery);
        }
        return controller.RedirectToAction(action, routeValues);
    }
    public static ListFilter Choice(string key, string label, params (string Value, string Label)[] values) => new(key, label, values.Select(x => new SelectListItem(x.Label, x.Value)).ToArray());
    public static void Filters(this Controller controller, params ListFilter[] filters) => controller.ViewData["ListFilters"] = filters;
    public static string Value(this Controller controller, string key) => controller.Request.Query[key].ToString().Trim()[..Math.Min(controller.Request.Query[key].ToString().Trim().Length, 100)];
    public static IQueryable<T> Dates<T>(this IQueryable<T> query, Controller controller, MedicalClock clock, string prefix = "") where T : MedicalRecord
    {
        if (DateOnly.TryParse(controller.Value(prefix + "from"), out var from)) { var start = clock.ToUtc(from.ToDateTime(TimeOnly.MinValue)); query = query.Where(x => x.LastUpdated >= start); }
        if (DateOnly.TryParse(controller.Value(prefix + "to"), out var to) && to < DateOnly.MaxValue) { var end = clock.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue)); query = query.Where(x => x.LastUpdated < end); }
        return query;
    }
}
