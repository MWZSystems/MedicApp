using MedicApp.Data;
using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trainify.Data;
namespace MedicApp.Controllers;

public class LeaveDocumentsController(MedicDbContext db, TrainifyDbContext shared, MedicalAccess access, LeaveRecordAccess records, LeaveEvidenceService evidence, ListReader reader, IOptions<DocumentOptions> options, Microsoft.Extensions.Localization.IStringLocalizer<UiText> text) : Controller
{
    private async Task<EvidencePage> PageAsync(int leaveId, CancellationToken ct)
    {
        var leave = await records.GetAsync(leaveId, false, ct);
        var names = await reader.NamesAsync([leave.PersonId], ct);
        var documents = await db.LeaveDocuments.AsNoTracking().Where(x => x.MedicalLeaveId == leaveId && x.Available).OrderByDescending(x => x.LastUpdated).ThenByDescending(x => x.Id)
            .Select(x => new EvidenceRow(x.Id, x.OriginalName, x.ContentType, x.ByteCount, x.LastUpdated)).ToListAsync(ct);
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
        TempData["Success"] = "EvidenceSaved";
        return RedirectToAction(nameof(Index), new { leaveId = input.LeaveId });
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
        page = Math.Clamp(page, 1, 100000);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.StartsAt).ThenByDescending(x => x.Id).Skip((page - 1) * 25).Take(25).ToListAsync(ct);
        var ids = items.Select(x => x.Id).ToArray();
        var counts = await db.LeaveDocuments.AsNoTracking().Where(x => ids.Contains(x.MedicalLeaveId) && x.Available).GroupBy(x => x.MedicalLeaveId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return View(new PatientFilePage { Person = person, CanSearch = canSearch, Records = new() { Items = items, Total = total, Page = page }, DocumentCounts = counts });
    }
}
