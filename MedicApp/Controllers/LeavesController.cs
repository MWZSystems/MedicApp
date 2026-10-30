using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace MedicApp.Controllers;

public class LeavesController(MedicDbContext db, MedicalAccess access, MedicalService service, ListReader reader, MedicalClock clock, LeaveCalendarReader calendar) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var doctor = await access.DoctorIdAsync(ct);
        var range = reader.Range(from, to);
        var query = db.Leaves.AsNoTracking().Where(x => x.Available && (x.PersonId == access.PersonId || x.DoctorId == doctor) && x.StartsAt < range.End && x.EndsAt > range.Start).OrderByDescending(x => x.StartsAt).ThenBy(x => x.Id);
        var model = await reader.PageAsync(query, page, null, from, to, ct);
        model.CurrentDoctorId = doctor;
        model.People = await reader.NamesAsync(model.Items.Select(x => x.PersonId), ct);
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
    [Authorize(Policy = "Doctor"), HttpGet]
    public IActionResult Create()
    {
        var today = clock.Local(clock.UtcNow).Date;
        return View(new LeaveForm { StartsAt = today, EndsAt = today.AddDays(1) });
    }
    [Authorize(Policy = "Doctor"), HttpPost]
    public async Task<IActionResult> Create(LeaveForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return View(input); }
        await service.CreateLeaveAsync(input.PersonId, input.StartsAt, input.EndsAt, input.Reference, input.AppointmentId, ct);
        TempData["Success"] = "Saved";
        return RedirectToAction(nameof(Index));
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
