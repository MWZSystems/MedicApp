using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace MedicApp.Controllers;

public class AppointmentsController(MedicDbContext db, MedicalAccess access, MedicalService service, ListReader reader, MedicalClock clock, IOptions<MedicalOptions> options, CalendarReader calendar, Microsoft.Extensions.Localization.IStringLocalizer<UiText> text) : Controller
{
    [HttpGet]
    public async Task<IActionResult> List(int page = 1, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
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
        return View("List", model);
    }
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? date = null, CancellationToken ct = default) => View(await CalendarPageAsync(date, false, ct));
    [HttpGet]
    public async Task<IActionResult> Book(DateTime? date, CancellationToken ct) => View("Index", await CalendarPageAsync(date == null ? null : DateOnly.FromDateTime(date.Value), true, ct));
    [HttpGet]
    public async Task<IActionResult> Events(DateOnly from, DateOnly to, CancellationToken ct) => Json(await calendar.EventsAsync(from, to, ct));
    [HttpGet]
    public async Task<IActionResult> Slots(DateOnly date, CancellationToken ct)
    {
        if (!ModelState.IsValid || date == default || date == DateOnly.MaxValue) { return BadRequest(); }
        var slots = await calendar.SlotsAsync(date, date.AddDays(1), ct);
        return Json(slots.Select(x => new { time = x.StartsAtLocal.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), durationMinutes = x.DurationMinutes, label = x.Label }));
    }
    [HttpPost]
    public async Task<IActionResult> Reserve(ReserveForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input.Date == null || input.Time == null || input.RequestKey == Guid.Empty) { throw new MedicalRuleException("InvalidReservation"); }
        var alreadyReserved = await db.Appointments.AsNoTracking().AnyAsync(x => x.RequestKey == input.RequestKey && x.PersonId == access.PersonId, ct);
        var id = await service.BookAtAsync(input.Date.Value.ToDateTime(input.Time.Value), input.RequestKey, ct);
        var message = alreadyReserved ? "FeedbackAlreadyReserved" : "BookingSaved";
        if (WantsJson()) { return Json(new { id, message = text[message].Value, kind = alreadyReserved ? "info" : "success" }); }
        if (alreadyReserved) { this.Info(message); } else { this.Success(message); }
        return RedirectToAction(nameof(Index), new { date = input.Date.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) });
    }
    private async Task<CalendarPage> CalendarPageAsync(DateOnly? date, bool openBooking, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.Local(clock.UtcNow));
        var selected = date ?? today;
        if (selected == DateOnly.MaxValue) { selected = today; }
        return new CalendarPage
        {
            Today = today,
            DemoBookingEnabled = options.Value.DemoBookingEnabled,
            BookingHorizonDays = options.Value.BookingHorizonDays,
            SlotMinutes = options.Value.DefaultSlotMinutes,
            Date = selected,
            LastBookingDate = DateOnly.FromDateTime(clock.Local(clock.UtcNow.AddDays(options.Value.BookingHorizonDays))),
            IsDoctor = await access.DoctorIdAsync(ct) != null,
            OpenBooking = openBooking,
            Slots = await calendar.SlotsAsync(selected, selected.AddDays(1), ct)
        };
    }
    private bool WantsJson() => Request.Headers.Accept.Any(x => x != null && x.Contains("application/json", StringComparison.OrdinalIgnoreCase));
    [HttpPost]
    public async Task<IActionResult> Book(BookingForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid || input.RequestKey == Guid.Empty) { return BadRequest(); }
        var alreadyReserved = await db.Appointments.AsNoTracking().AnyAsync(x => x.RequestKey == input.RequestKey && x.PersonId == access.PersonId, ct);
        await service.BookAsync(input.WindowId, input.StartsAt, input.RequestKey, ct);
        if (alreadyReserved) { this.Info("FeedbackAlreadyReserved"); } else { this.Success("BookingSaved"); }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> Change(ChangeForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return BadRequest(); }
        await service.ChangeAppointmentAsync(input.Id, (AppointmentStatus)input.Status, input.Reason, input.Version, ct);
        var cancelled = (AppointmentStatus)input.Status == AppointmentStatus.Cancelled;
        var message = cancelled ? "FeedbackAppointmentCancelled" : "FeedbackAppointmentUpdated";
        if (WantsJson()) { return Json(new { message = text[message].Value, kind = cancelled ? "warning" : "success" }); }
        if (cancelled) { this.Warning(message); } else { this.Success(message); }
        return RedirectToAction(nameof(Index));
    }
}
