using System.Data;
using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Trainify.Data;
namespace MedicApp.Controllers;

public class AdminController(MedicDbContext db, TrainifyDbContext shared, MedicalAccess access, MedicalService service, ListReader reader, MedicalClock clock) : Controller
{
    [Authorize(Policy = "Doctors"), HttpGet]
    public async Task<IActionResult> Doctors(int page = 1, CancellationToken ct = default)
    {
        var model = await reader.PageAsync(db.Doctors.AsNoTracking().OrderBy(x => x.Id), page, null, null, null, ct);
        model.People = await reader.NamesAsync(model.Items.Select(x => x.PersonId), ct);
        return View(model);
    }
    [Authorize(Policy = "Doctors"), HttpPost]
    public async Task<IActionResult> Doctor(DoctorForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return BadRequest(); }
        if (!await shared.People.AsNoTracking().AnyAsync(x => x.Id == input.PersonId && x.Active, ct)) { throw new MedicalRuleException("NotFound"); }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var doctor = input.Id == 0 ? new Doctor { PersonId = input.PersonId } : await db.Doctors.SingleOrDefaultAsync(x => x.Id == input.Id, ct) ?? throw new MedicalRuleException("NotFound");
        if (input.Id != 0)
        {
            if (doctor.PersonId != input.PersonId) { throw new MedicalRuleException("Forbidden"); }
            service.SetVersion(doctor, input.Version);
            if (!input.Available && await db.Appointments.AnyAsync(x => x.DoctorId == doctor.Id && x.Available && x.Status == AppointmentStatus.Scheduled && x.EndsAt > clock.UtcNow, ct)) { throw new MedicalRuleException("ExistingAppointments"); }
        }
        doctor.Specialty = input.Specialty.Trim();
        doctor.ProfessionalLicense = input.ProfessionalLicense.Trim();
        doctor.Available = input.Available;
        service.Stamp(doctor);
        if (input.Id == 0) { db.Doctors.Add(doctor); }
        await db.SaveChangesAsync(ct);
        service.Audit("Doctor", doctor.Id, input.Id == 0 ? "Create" : "Update");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        TempData["Success"] = "Saved";
        return RedirectToAction(nameof(Doctors));
    }
    [Authorize(Policy = "Access"), HttpGet]
    public async Task<IActionResult> Access(int page = 1, CancellationToken ct = default)
    {
        var model = await reader.PageAsync(db.AccessProfiles.AsNoTracking().OrderBy(x => x.Id), page, null, null, null, ct);
        var ids = model.Items.Select(x => x.UserId).ToArray();
        ViewBag.AccountNames = await shared.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.UserName ?? x.Id, ct);
        return View(model);
    }
    [Authorize(Policy = "Access"), HttpGet]
    public async Task<IActionResult> Accounts(string? term, CancellationToken ct)
    {
        term = term?.Trim();
        if (term == null || term.Length < 2 || term.Length > 80) { return Json(new { results = Array.Empty<object>() }); }
        return Json(new { results = await shared.Users.AsNoTracking().Where(x => x.Active && x.Person.Active && x.UserName != null && x.UserName.Contains(term)).OrderBy(x => x.UserName).Take(20).Select(x => new { id = x.Id, text = x.UserName }).ToListAsync(ct) });
    }
    [Authorize(Policy = "Access"), HttpPost]
    public async Task<IActionResult> Access(AccessForm input, CancellationToken ct)
    {
        if (!ModelState.IsValid) { return BadRequest(); }
        if (!await shared.Users.AsNoTracking().AnyAsync(x => x.Id == input.UserId && x.Active && x.Person.Active, ct)) { throw new MedicalRuleException("NotFound"); }
        if (input.UserId == access.UserId && !input.ManageAccess) { throw new MedicalRuleException("SelfRevoke"); }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var profile = await db.AccessProfiles.SingleOrDefaultAsync(x => x.UserId == input.UserId, ct);
        if (profile == null) { profile = new AccessProfile { UserId = input.UserId }; db.AccessProfiles.Add(profile); }
        else { service.SetVersion(profile, input.Version); }
        profile.Available = true;
        profile.ManageAccess = input.ManageAccess;
        profile.ManageDoctors = input.ManageDoctors;
        profile.ReadIntegration = input.ReadIntegration;
        service.Stamp(profile);
        await db.SaveChangesAsync(ct);
        service.Audit("AccessProfile", profile.Id, "SetPermissions");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        TempData["Success"] = "Saved";
        return RedirectToAction(nameof(Access));
    }
    [Authorize(Policy = "Access"), HttpGet]
    public async Task<IActionResult> Audit(int page = 1, CancellationToken ct = default)
    {
        var model = await reader.PageAsync(db.Audit.AsNoTracking().OrderByDescending(x => x.Id), page, null, null, null, ct);
        var ids = model.Items.Select(x => x.UpdatedById).ToArray();
        ViewBag.AccountNames = await shared.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.UserName ?? x.Id, ct);
        return View(model);
    }
}
