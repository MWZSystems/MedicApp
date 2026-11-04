using MedicApp.Data;
using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trainify.Data;
namespace MedicApp.Controllers;

public class LeaveDocumentsController(MedicDbContext db, TrainifyDbContext shared, MedicalAccess access, LeaveRecordAccess records, LeaveEvidenceService evidence, ListReader reader, MedicalClock clock, IOptions<DocumentOptions> options, Microsoft.Extensions.Localization.IStringLocalizer<UiText> text) : Controller
{
    private async Task<EvidencePage> PageAsync(int leaveId, CancellationToken ct)
    {
        var leave = await records.GetAsync(leaveId, false, ct);
        var names = await reader.NamesAsync([leave.PersonId], ct);
        var query = db.LeaveDocuments.AsNoTracking().Where(x => x.MedicalLeaveId == leaveId && x.Available).Dates(this, clock);
        var search = this.Search(); if (search != null) { query = query.Where(x => x.OriginalName.Contains(search)); }
        var type = this.Value("type"); if (type == "pdf") { query = query.Where(x => x.ContentType == "application/pdf"); } else if (type == "image") { query = query.Where(x => x.ContentType.StartsWith("image/")); }
        this.Filters(ListFiltering.Choice("type", "ListContentType", ("pdf", "ListPdf"), ("image", "ListImages")), new("from", "From", Date: true), new("to", "To", Date: true));
        var documents = await query.OrderByDescending(x => x.LastUpdated).ThenByDescending(x => x.Id).Select(x => new EvidenceRow(x.Id, x.OriginalName, x.ContentType, x.ByteCount, x.LastUpdated)).ToPageAsync(this);
        return new EvidencePage { Leave = leave, Person = names[leave.PersonId], CanUpload = await records.CanUploadAsync(leave, ct), Documents = documents, Limits = options.Value };
    }
    [HttpGet]
    public async Task<IActionResult> Index(int leaveId, CancellationToken ct)
    {
        if (!ModelState.IsValid || leaveId <= 0) { return BadRequest(); }
        return View(await PageAsync(leaveId, ct));
    }
    [HttpPost, RequestSizeLimit(25165824), RequestFormLimits(MultipartBodyLengthLimit = 25165824)]
    public async Task<IActionResult> Upload(EvidenceUploadForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return BadRequest(); }
        try { await evidence.UploadAsync(input.LeaveId, input.RequestKey, input.Files, ct); }
        catch (MedicalRuleException error) when (error.Message is "EvidenceLimits" or "EvidenceInvalid")
        {
            ModelState.AddModelError("", text[error.Message]);
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return View("Index", await PageAsync(input.LeaveId, ct));
        }
        this.Success("EvidenceSaved");
        var back = this.RedirectBack(nameof(Index)); return back is RedirectToActionResult ? RedirectToAction(nameof(Index), new { leaveId = input.LeaveId }) : back;
    }
    [HttpGet]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var item = await evidence.DownloadAsync(id, ct);
        return File(item.Bytes, item.Document.ContentType, item.Document.OriginalName);
    }
    [HttpGet]
    public async Task<IActionResult> Record(int? personId, int page = 1, CancellationToken ct = default)
    {
        if (!ModelState.IsValid || personId <= 0) { return BadRequest(); }
        var canSearch = await records.CanRegisterAsync(ct);
        if (!canSearch && personId == null) { personId = access.PersonId; }
        if (personId == null) { return View(new PatientFilePage { CanSearch = canSearch }); }
        var query = await records.PersonQueryAsync(personId.Value, ct);
        var person = await shared.People.AsNoTracking().Where(x => x.Id == personId).Select(x => new LeaveCalendarPerson(x.Id, x.FirstName + " " + (x.LastName ?? ""), x.EmployeeNumber)).SingleOrDefaultAsync(ct) ?? throw new MedicalRuleException("NotFound");
        var search = this.Search(); if (search != null) { query = query.Where(x => x.Reference.Contains(search)); }
        if (int.TryParse(this.Value("state"), out var state) && Enum.IsDefined(typeof(Models.Entities.LeaveStatus), state)) { query = query.Where(x => (int)x.Status == state); }
        if (DateOnly.TryParse(this.Value("from"), out var from)) { var start = clock.ToUtc(from.ToDateTime(TimeOnly.MinValue)); query = query.Where(x => x.EndsAt > start); }
        if (DateOnly.TryParse(this.Value("to"), out var to) && to < DateOnly.MaxValue) { var end = clock.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue)); query = query.Where(x => x.StartsAt < end); }
        this.Filters(ListFiltering.Choice("state", "Status", Enum.GetValues<Models.Entities.LeaveStatus>().Select(x => (((int)x).ToString(System.Globalization.CultureInfo.InvariantCulture), x.ToString())).ToArray()), new("from", "From", Date: true), new("to", "To", Date: true));
        var items = await query.OrderByDescending(x => x.StartsAt).ThenByDescending(x => x.Id).ToPageAsync(this);
        var info = (PageInfo)ViewData[Paging.ViewDataKey()]!;
        var ids = items.Select(x => x.Id).ToArray();
        var counts = await db.LeaveDocuments.AsNoTracking().Where(x => ids.Contains(x.MedicalLeaveId) && x.Available).GroupBy(x => x.MedicalLeaveId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return View(new PatientFilePage { Person = person, CanSearch = canSearch, Records = new() { Items = items, Total = info.Total, Page = info.Number }, DocumentCounts = counts });
    }
}
