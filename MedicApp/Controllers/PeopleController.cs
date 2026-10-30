using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Trainify.Data;
namespace MedicApp.Controllers;

public class PeopleController(TrainifyDbContext shared) : Controller
{
    [HttpGet, Authorize(Policy = "ClinicalOrAdmin")]
    public async Task<IActionResult> Search(string? term, CancellationToken ct)
    {
        term = term?.Trim();
        if (term == null || term.Length < 2 || term.Length > 80) { return Json(new { results = Array.Empty<object>() }); }
        var rows = await shared.People.AsNoTracking().Where(x => x.Active && (x.EmployeeNumber.Contains(term) || x.FirstName.Contains(term) || x.LastName != null && x.LastName.Contains(term))).OrderBy(x => x.EmployeeNumber).ThenBy(x => x.Id).Take(20).Select(x => new { id = x.Id, text = x.EmployeeNumber + " · " + x.FirstName + " " + (x.LastName ?? "") }).ToListAsync(ct);
        return Json(new { results = rows });
    }
}
