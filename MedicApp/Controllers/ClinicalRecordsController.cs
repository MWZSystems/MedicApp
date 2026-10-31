using MedicApp.Data;
using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Trainify.Data;
namespace MedicApp.Controllers;

[Authorize(Policy = "ClinicalRecords")]
public class ClinicalRecordsController(MedicDbContext db, TrainifyDbContext shared, ClinicalRecordService records, MedicalService medical, MedicalClock clock, IOptions<DocumentOptions> options, IStringLocalizer<UiText> text) : Controller
{
    private async Task<ClinicalRecordPage> PageAsync(int? personId, int visitsPage, int filesPage, CancellationToken ct)
    {
        if (personId == null) { return new() { Limits = options.Value }; }
        var person = await records.PersonAsync(personId.Value, false, ct);
        visitsPage = Math.Clamp(visitsPage, 1, 100000); filesPage = Math.Clamp(filesPage, 1, 100000);
        var visits = db.ClinicalVisits.AsNoTracking().Where(x => x.PersonId == personId && x.Available);
        var documents = db.PatientDocuments.AsNoTracking().Where(x => x.PersonId == personId && x.Available);
        medical.Audit("PatientRecord", personId.Value, "Read"); await db.SaveChangesAsync(ct);
        var result = new ClinicalRecordPage
        {
            Person = person,
            Limits = options.Value,
            Visits = new() { Page = visitsPage, Total = await visits.CountAsync(ct), Items = await visits.OrderByDescending(x => x.VisitedAt).ThenByDescending(x => x.Id).Skip((visitsPage - 1) * 10).Take(10).ToListAsync(ct) },
            Documents = new() { Page = filesPage, Total = await documents.CountAsync(ct), Items = await documents.OrderByDescending(x => x.Id).Skip((filesPage - 1) * 25).Take(25).ToListAsync(ct) }
        };
        var users = result.Visits.Items.Select(x => x.UpdatedById).Concat(result.Documents.Items.Select(x => x.UpdatedById)).Distinct().ToArray();
        result.AccountNames = await shared.Users.AsNoTracking().Where(x => users.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.UserName ?? "", ct);
        return result;
    }
    [HttpGet]
    public async Task<IActionResult> Index(int? personId, int visitsPage = 1, int filesPage = 1, int? visitId = null, CancellationToken ct = default)
    {
        if (!ModelState.IsValid || personId <= 0) { return BadRequest(); }
        var model = await PageAsync(personId, visitsPage, filesPage, ct);
        if (visitId != null) { model.SelectedVisit = await db.ClinicalVisits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == visitId && x.PersonId == personId && x.Available, ct) ?? throw new MedicalRuleException("NotFound"); }
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> People(string? term, CancellationToken ct)
    {
        term = term?.Trim();
        if (term == null || term.Length < 2 || term.Length > 80) { return Json(new { results = Array.Empty<object>() }); }
        return Json(new { results = await shared.People.AsNoTracking().Where(x => x.EmployeeNumber.Contains(term) || (x.FirstName + " " + (x.LastName ?? "")).Contains(term)).OrderBy(x => x.EmployeeNumber).ThenBy(x => x.Id).Take(20).Select(x => new { id = x.Id, text = x.EmployeeNumber + " · " + x.FirstName + " " + (x.LastName ?? "") }).ToListAsync(ct) });
    }
    [HttpGet]
    public async Task<IActionResult> Create(int personId, CancellationToken ct)
    {
        ViewBag.Person = await records.PersonAsync(personId, true, ct);
        return View(new ClinicalVisitForm { PersonId = personId, VisitedAt = clock.Local(clock.UtcNow) });
    }
    [HttpPost]
    public async Task<IActionResult> Create(ClinicalVisitForm input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await records.AddVisitAsync(input, ct); TempData["Success"] = "Saved";
                return RedirectToAction(nameof(Index), new { personId = input.PersonId });
            }
            catch (MedicalRuleException error) when (error.Message is "InvalidClinicalInput" or "InvalidClinicalDate" or "InvalidBloodPressure") { ModelState.AddModelError("", text[error.Message]); }
        }
        ViewBag.Person = await records.PersonAsync(input.PersonId, true, ct);
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity; return View(input);
    }
    [HttpPost, RequestSizeLimit(25165824), RequestFormLimits(MultipartBodyLengthLimit = 25165824)]
    public async Task<IActionResult> Upload(PatientUploadForm input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await records.UploadAsync(input, ct); TempData["Success"] = "EvidenceSaved";
                return RedirectToAction(nameof(Index), new { personId = input.PersonId });
            }
            catch (MedicalRuleException error) when (error.Message is "EvidenceLimits" or "EvidenceInvalid" or "InvalidClinicalInput") { ModelState.AddModelError("", text[error.Message]); }
        }
        Response.StatusCode = StatusCodes.Status422UnprocessableEntity; var model = await PageAsync(input.PersonId, 1, 1, ct);
        if (input.ClinicalVisitId != null) { model.SelectedVisit = await db.ClinicalVisits.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.ClinicalVisitId && x.PersonId == input.PersonId && x.Available, ct) ?? throw new MedicalRuleException("NotFound"); }
        return View("Index", model);
    }
    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var item = await records.DownloadAsync(id, ct); return File(item.Bytes, item.Document.ContentType, item.Document.OriginalName);
    }
}
