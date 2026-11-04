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
        var visits = db.ClinicalVisits.AsNoTracking().Where(x => x.PersonId == personId && x.Available);
        var documents = db.PatientDocuments.AsNoTracking().Where(x => x.PersonId == personId && x.Available);
        ViewBag.VisitTotal = await visits.CountAsync(ct); ViewBag.DocumentTotal = await documents.CountAsync(ct);
        var visitSearch = this.Search("visits"); if (visitSearch != null) { visits = visits.Where(x => x.Reason.Contains(visitSearch) || x.Diagnosis.Contains(visitSearch) || x.Treatment.Contains(visitSearch) || x.Prescription.Contains(visitSearch) || x.Findings.Contains(visitSearch) || x.Notes.Contains(visitSearch)); }
        var fileSearch = this.Search("files"); if (fileSearch != null) { documents = documents.Where(x => x.OriginalName.Contains(fileSearch) || x.Description.Contains(fileSearch)); }
        if (DateOnly.TryParse(this.Value("visitsfrom"), out var from)) { var start = clock.ToUtc(from.ToDateTime(TimeOnly.MinValue)); visits = visits.Where(x => x.VisitedAt >= start); }
        if (DateOnly.TryParse(this.Value("visitsto"), out var to) && to < DateOnly.MaxValue) { var end = clock.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue)); visits = visits.Where(x => x.VisitedAt < end); }
        documents = documents.Dates(this, clock, "files");
        if (int.TryParse(this.Value("fileskind"), out var kind) && Enum.IsDefined(typeof(Models.Entities.PatientDocumentKind), kind)) { documents = documents.Where(x => (int)x.Kind == kind); }
        ViewData["VisitFilters"] = new ListFilter[] { new("visitsfrom", "From", Date: true), new("visitsto", "To", Date: true) };
        ViewData["FileFilters"] = new ListFilter[] { ListFiltering.Choice("fileskind", "DocumentKind", Enum.GetValues<Models.Entities.PatientDocumentKind>().Select(x => (((int)x).ToString(System.Globalization.CultureInfo.InvariantCulture), "PatientFile" + x)).ToArray()), new("filesfrom", "From", Date: true), new("filesto", "To", Date: true) };
        var visitRows = await visits.OrderByDescending(x => x.VisitedAt).ThenByDescending(x => x.Id).ToPageAsync(this, "visits");
        var fileRows = await documents.OrderByDescending(x => x.Id).ToPageAsync(this, "files");
        var visitInfo = (PageInfo)ViewData[Paging.ViewDataKey("visits")]!; var fileInfo = (PageInfo)ViewData[Paging.ViewDataKey("files")]!;
        medical.Audit("PatientRecord", personId.Value, "Read"); await db.SaveChangesAsync(ct);
        var result = new ClinicalRecordPage
        {
            Person = person,
            Limits = options.Value,
            Visits = new() { Page = visitInfo.Number, Total = visitInfo.Total, Items = visitRows },
            Documents = new() { Page = fileInfo.Number, Total = fileInfo.Total, Items = fileRows }
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
                await records.AddVisitAsync(input, ct); this.Success("FeedbackVisitSaved");
                var back = this.RedirectBack(nameof(Index)); return back is RedirectToActionResult ? RedirectToAction(nameof(Index), new { personId = input.PersonId }) : back;
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
                await records.UploadAsync(input, ct); this.Success("EvidenceSaved");
                var back = this.RedirectBack(nameof(Index)); return back is RedirectToActionResult ? RedirectToAction(nameof(Index), new { personId = input.PersonId }) : back;
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
