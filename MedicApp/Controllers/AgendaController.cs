using MedicApp.Data;
using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace MedicApp.Controllers;

[Authorize(Policy = "Doctor")]
public class AgendaController(MedicDbContext db, MedicalAccess access, MedicalService service, MedicalClock clock, Microsoft.Extensions.Options.IOptions<MedicalOptions> options) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var doctor = await access.DoctorIdAsync(ct);
        var query = db.Windows.AsNoTracking().Where(x => x.DoctorId == doctor);
        var active = this.Value("active"); if (active != "all") { var available = active != "false"; query = query.Where(x => x.Available == available); }
        var period = this.Value("period"); if (period == "past") { query = query.Where(x => x.EndsAt <= clock.UtcNow); } else if (period != "all") { query = query.Where(x => x.EndsAt > clock.UtcNow); }
        var search = this.Search(); if (search != null) { query = int.TryParse(search, out var slot) ? query.Where(x => x.SlotMinutes == slot) : query.Where(x => false); }
        this.Filters(ListFiltering.Choice("active", "ListAvailability", ("all", "ListAll"), ("true", "ListActive"), ("false", "ListInactive")) with { DefaultValue = "true" }, ListFiltering.Choice("period", "ListPeriod", ("all", "ListAll"), ("future", "ListFuture"), ("past", "ListPast")) with { DefaultValue = "future" });
        ViewBag.Windows = await query.OrderBy(x => x.StartsAt).ThenBy(x => x.Id).ToPageAsync(this);
        var tomorrow = clock.Local(clock.UtcNow).Date.AddDays(1);
        return View(new WindowForm { StartsAt = tomorrow, EndsAt = tomorrow.AddMinutes(1440 / options.Value.DefaultSlotMinutes * options.Value.DefaultSlotMinutes), SlotMinutes = options.Value.DefaultSlotMinutes });
    }
    [HttpPost]
    public async Task<IActionResult> Create(WindowForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return BadRequest(); }
        await service.AddWindowAsync(input.StartsAt, input.EndsAt, input.SlotMinutes, ct);
        this.Success("FeedbackAgendaSaved");
        return this.RedirectBack(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> Disable(ChangeForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return BadRequest(); }
        var doctor = await access.DoctorIdAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var item = await db.Windows.SingleOrDefaultAsync(x => x.Id == input.Id && x.DoctorId == doctor && x.Available, ct) ?? throw new MedicalRuleException("NotFound");
        if (await db.Appointments.AnyAsync(x => x.AvailabilityWindowId == item.Id && x.Available && x.Status == Models.Entities.AppointmentStatus.Scheduled, ct)) { throw new MedicalRuleException("ExistingAppointments"); }
        service.SetVersion(item, input.Version);
        item.Available = false;
        service.Stamp(item);
        service.Audit("AvailabilityWindow", item.Id, "Disable");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        this.Success("FeedbackAgendaDisabled");
        return this.RedirectBack(nameof(Index));
    }
}
