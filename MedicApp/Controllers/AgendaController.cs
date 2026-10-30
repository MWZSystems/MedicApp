using MedicApp.Data;
using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace MedicApp.Controllers;

[Authorize(Policy = "Doctor")]
public class AgendaController(MedicDbContext db, MedicalAccess access, MedicalService service, MedicalClock clock) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var doctor = await access.DoctorIdAsync(ct);
        ViewBag.Windows = await db.Windows.AsNoTracking().Where(x => x.DoctorId == doctor && x.Available && x.EndsAt > clock.UtcNow).OrderBy(x => x.StartsAt).Take(100).ToListAsync(ct);
        var tomorrow = clock.Local(clock.UtcNow).Date.AddDays(1);
        return View(new WindowForm { StartsAt = tomorrow.AddHours(8), EndsAt = tomorrow.AddHours(16) });
    }
    [HttpPost]
    public async Task<IActionResult> Create(WindowForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return BadRequest(); }
        await service.AddWindowAsync(input.StartsAt, input.EndsAt, input.SlotMinutes, ct);
        TempData["Success"] = "Saved";
        return RedirectToAction(nameof(Index));
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
        return RedirectToAction(nameof(Index));
    }
}
