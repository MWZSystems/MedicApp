using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace MedicApp.Controllers;

public class LeavesController(MedicDbContext db, MedicalAccess access, Trainify.Data.TrainifyDbContext shared, MedicalService service, ListReader reader, MedicalClock clock, LeaveCalendarReader calendar, LeaveRecordAccess records, Microsoft.Extensions.Localization.IStringLocalizer<UiText> text) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var doctor = await access.DoctorIdAsync(ct);
        var administrator = await records.IsAdministratorAsync(ct);
        ViewBag.CanRegister = await records.CanRegisterAsync(ct);
        var range = reader.Range(from, to);
        var query = db.Leaves.AsNoTracking().Where(x => x.Available && (administrator || x.PersonId == access.PersonId || x.DoctorId == doctor) && x.StartsAt < range.End && x.EndsAt > range.Start).OrderByDescending(x => x.StartsAt).ThenBy(x => x.Id);
        var model = await reader.PageAsync(query, page, null, from, to, ct);
        model.CurrentDoctorId = doctor;
        model.People = await reader.NamesAsync(model.Items.Select(x => x.PersonId), ct);
        var ids = model.Items.Select(x => x.Id).ToArray();
        ViewBag.DocumentCounts = await db.LeaveDocuments.AsNoTracking().Where(x => ids.Contains(x.MedicalLeaveId) && x.Available).GroupBy(x => x.MedicalLeaveId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Calendar(int? personId, CancellationToken ct)
    {
        if (!ModelState.IsValid || personId <= 0) { return BadRequest(); }
        return View(await calendar.PageAsync(personId, ct));
    }
    [HttpGet]
    public async Task<IActionResult> CalendarPeople(string? term, CancellationToken ct) => Json(new { results = (await calendar.SearchAsync(term, ct)).Select(x => new { id = x.Id, text = x.Label }) });
    [HttpGet]
    public async Task<IActionResult> CalendarEvents(int personId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (!ModelState.IsValid || personId <= 0 || from == default || to == DateOnly.MaxValue) { return BadRequest(); }
        return Json(await calendar.EventsAsync(personId, from, to, ct));
    }
    private async Task PrepareCreateAsync(LeaveForm input, CancellationToken ct)
    {
        ViewBag.IsDoctor = await access.DoctorIdAsync(ct) != null;
        ViewBag.CanManageDoctors = await access.CanManageDoctorsAsync(ct);
        ViewBag.SelectedPerson = input.PersonId > 0 ? (await reader.NamesAsync([input.PersonId], ct)).GetValueOrDefault(input.PersonId) : null;
        if (input.DoctorId > 0)
        {
            var personId = await db.Doctors.Where(x => x.Id == input.DoctorId).Select(x => x.PersonId).SingleOrDefaultAsync(ct);
            ViewBag.SelectedDoctor = (await reader.NamesAsync([personId], ct)).GetValueOrDefault(personId);
        }
    }
    [Authorize(Policy = "LeaveEntry"), HttpGet]
    public async Task<IActionResult> ResponsibleDoctors(string? term, CancellationToken ct)
    {
        term = term?.Trim();
        if (term == null || term.Length < 2 || term.Length > 80) { return Json(new { results = Array.Empty<object>() }); }
        var doctors = await db.Doctors.AsNoTracking().Where(x => x.Available).Select(x => new { x.Id, x.PersonId }).ToListAsync(ct);
        var ids = doctors.Select(x => x.PersonId).ToArray();
        var people = await shared.People.AsNoTracking().Where(x => ids.Contains(x.Id) && x.Active && !x.EmployeeNumber.StartsWith(DemoDataService.Prefix) && (x.EmployeeNumber.Contains(term) || (x.FirstName + " " + (x.LastName ?? "")).Contains(term)))
            .OrderBy(x => x.EmployeeNumber).Take(20).Select(x => new { x.Id, Name = x.EmployeeNumber + " · " + x.FirstName + " " + (x.LastName ?? "") }).ToListAsync(ct);
        return Json(new { results = people.Select(x => new { id = doctors.Single(d => d.PersonId == x.Id).Id, text = x.Name }) });
    }
    [Authorize(Policy = "LeaveEntry"), HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var today = clock.Local(clock.UtcNow).Date;
        var input = new LeaveForm { StartsAt = today, EndsAt = today.AddDays(1) };
        await PrepareCreateAsync(input, ct); return View(input);
    }
    [Authorize(Policy = "LeaveEntry"), HttpPost]
    public async Task<IActionResult> Create(LeaveForm input, CancellationToken ct)
    {
        await PrepareCreateAsync(input, ct);
        if (!(bool)ViewBag.IsDoctor && input.DoctorId == null) { ModelState.AddModelError(nameof(input.DoctorId), text["ResponsibleDoctorRequired"]); }
        if (!ModelState.IsValid) { return View(input); }
        var id = await service.CreateLeaveAsync(input.PersonId, input.StartsAt, input.EndsAt, input.Reference, input.AppointmentId, ct, input.DoctorId);
        TempData["Success"] = "LeaveDraftSaved";
        return RedirectToAction("Index", "LeaveDocuments", new { leaveId = id });
    }
    [Authorize(Policy = "Doctor"), HttpPost]
    public async Task<IActionResult> Change(ChangeForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return BadRequest(); }
        await service.ChangeLeaveAsync(input.Id, (LeaveStatus)input.Status, input.Reason, input.Version, ct);
        TempData["Success"] = "Saved";
        return RedirectToAction(nameof(Index));
    }
}
