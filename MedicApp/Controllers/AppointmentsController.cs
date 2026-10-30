using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace MedicApp.Controllers;

public class AppointmentsController(MedicDbContext db, MedicalAccess access, MedicalService service, ListReader reader, MedicalClock clock, IOptions<MedicalOptions> options) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var doctor = await access.DoctorIdAsync(ct);
        var range = reader.Range(from, to);
        var query = db.Appointments.AsNoTracking().Where(x => x.Available && (x.PersonId == access.PersonId || x.DoctorId == doctor) && x.StartsAt < range.End && x.EndsAt > range.Start).OrderByDescending(x => x.StartsAt).ThenBy(x => x.Id);
        var model = await reader.PageAsync(query, page, null, from, to, ct);
        model.CurrentDoctorId = doctor;
        model.People = await reader.NamesAsync(model.Items.Select(x => x.PersonId), ct);
        var doctors = await db.Doctors.AsNoTracking().Where(x => model.Items.Select(a => a.DoctorId).Contains(x.Id)).Select(x => new { x.Id, x.PersonId }).ToListAsync(ct);
        var names = await reader.NamesAsync(doctors.Select(x => x.PersonId), ct);
        model.Doctors = doctors.ToDictionary(x => x.Id, x => names[x.PersonId]);
        return View(model);
    }
    [HttpGet]
    public async Task<IActionResult> Book(DateTime? date, CancellationToken ct)
    {
        var day = (date ?? clock.Local(clock.UtcNow)).Date;
        var start = clock.ToUtc(day);
        var end = clock.ToUtc(day.AddDays(1));
        ViewBag.Date = day;
        if (start > clock.UtcNow.AddDays(options.Value.BookingHorizonDays) || end <= clock.UtcNow) { return View(new List<SlotOption>()); }
        var windows = await (from w in db.Windows.AsNoTracking() join d in db.Doctors.AsNoTracking() on w.DoctorId equals d.Id where w.Available && d.Available && w.StartsAt < end && w.EndsAt > start orderby w.StartsAt select new { Window = w, d.PersonId, d.Specialty }).Take(200).ToListAsync(ct);
        var doctorIds = windows.Select(x => x.Window.DoctorId).Distinct().ToArray();
        var busy = await db.Appointments.AsNoTracking().Where(x => x.Available && x.Status == AppointmentStatus.Scheduled && (doctorIds.Contains(x.DoctorId) || x.PersonId == access.PersonId) && x.StartsAt < end && x.EndsAt > start).Select(x => new { x.DoctorId, x.PersonId, x.StartsAt, x.EndsAt }).ToListAsync(ct);
        var names = await reader.NamesAsync(windows.Select(x => x.PersonId), ct);
        var slots = new List<SlotOption>();
        foreach (var row in windows)
        {
            var w = row.Window;
            for (var at = w.StartsAt; at.AddMinutes(w.SlotMinutes) <= w.EndsAt && slots.Count < 500; at = at.AddMinutes(w.SlotMinutes))
            {
                if (at < start || at >= end || at <= clock.UtcNow || at > clock.UtcNow.AddDays(options.Value.BookingHorizonDays)) { continue; }
                if (busy.Any(x => (x.DoctorId == w.DoctorId || x.PersonId == access.PersonId) && MedicalRules.Overlaps(at, at.AddMinutes(w.SlotMinutes), x.StartsAt, x.EndsAt))) { continue; }
                slots.Add(new(w.Id, w.DoctorId, names[row.PersonId], row.Specialty, clock.Local(at), w.SlotMinutes));
            }
        }
        return View(slots);
    }
    [HttpPost]
    public async Task<IActionResult> Book(BookingForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input.RequestKey == Guid.Empty) { return BadRequest(); }
        await service.BookAsync(input.WindowId, input.StartsAt, input.RequestKey, ct);
        TempData["Success"] = "BookingSaved";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> Change(ChangeForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return BadRequest(); }
        await service.ChangeAppointmentAsync(input.Id, (AppointmentStatus)input.Status, input.Reason, input.Version, ct);
        TempData["Success"] = "Saved";
        return RedirectToAction(nameof(Index));
    }
}
