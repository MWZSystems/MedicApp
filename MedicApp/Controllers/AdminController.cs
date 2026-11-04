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
        var search = this.Search(); var query = db.Doctors.AsNoTracking();
        if (search != null) { var people = ListFiltering.People(db, search); query = query.Where(x => x.Specialty.Contains(search) || x.ProfessionalLicense.Contains(search) || people.Contains(x.PersonId)); }
        if (bool.TryParse(this.Value("active"), out var active)) { query = query.Where(x => x.Available == active); }
        if (bool.TryParse(this.Value("recurring"), out var recurring)) { query = query.Where(x => x.UseRecurringSchedule == recurring); }
        this.Filters(ListFiltering.Choice("active", "ListAvailability", ("true", "ListActive"), ("false", "ListInactive")), ListFiltering.Choice("recurring", "ListRecurring", ("true", "ListAutomatic"), ("false", "ListManual")));
        var model = await reader.PageAsync(query.OrderBy(x => x.Id), this, search, null, null, ct);
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
        doctor.UseRecurringSchedule = input.UseRecurringSchedule;
        doctor.Specialty = input.Specialty.Trim();
        doctor.ProfessionalLicense = input.ProfessionalLicense.Trim();
        doctor.Available = input.Available;
        service.Stamp(doctor);
        if (input.Id == 0) { db.Doctors.Add(doctor); }
        await db.SaveChangesAsync(ct);
        service.Audit("Doctor", doctor.Id, input.Id == 0 ? "Create" : "Update");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if (input.Available) { this.Success("FeedbackDoctorSaved"); } else { this.Warning("FeedbackDoctorDisabled"); }
        return this.RedirectBack(nameof(Doctors));
    }
    [Authorize(Policy = "Access"), HttpGet]
    public async Task<IActionResult> Access(int page = 1, CancellationToken ct = default)
    {
        var search = this.Search(); var query = db.AccessProfiles.AsNoTracking();
        if (search != null) { var accounts = ListFiltering.Accounts(db, search); query = query.Where(x => accounts.Contains(x.UserId)); }
        if (bool.TryParse(this.Value("active"), out var active)) { query = query.Where(x => x.Available == active); }
        query = this.Value("permission") switch { "doctors" => query.Where(x => x.ManageDoctors), "access" => query.Where(x => x.ManageAccess), "clinical" => query.Where(x => x.ManageClinicalRecords), "inventory" => query.Where(x => x.ManageInventory), "integration" => query.Where(x => x.ReadIntegration), _ => query };
        this.Filters(ListFiltering.Choice("active", "ListAvailability", ("true", "ListActive"), ("false", "ListInactive")), ListFiltering.Choice("permission", "ListPermission", ("doctors", "ManageDoctors"), ("access", "ManageAccess"), ("clinical", "ManageClinicalRecords"), ("inventory", "ManageInventory"), ("integration", "ReadIntegration")));
        var model = await reader.PageAsync(query.OrderBy(x => x.Id), this, search, null, null, ct);
        var ids = model.Items.Select(x => x.UserId).ToArray();
        ViewBag.AccountNames = await shared.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.UserName ?? x.Id, ct);
        return View(model);
    }
    [Authorize(Policy = "Access"), HttpGet]
    public async Task<IActionResult> Accounts(string? term, CancellationToken ct)
    {
        term = term?.Trim();
        if (term == null || term.Length < 2 || term.Length > 80) { return Json(new { results = Array.Empty<object>() }); }
        return Json(new { results = await shared.Users.AsNoTracking().Where(x => x.Active && x.Person.Active && x.UserName != null && x.UserName.Contains(term)).OrderBy(x => x.UserName).ThenBy(x => x.Id).Take(20).Select(x => new { id = x.Id, text = x.UserName }).ToListAsync(ct) });
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
        profile.ManageClinicalRecords = input.ManageClinicalRecords;
        profile.ManageInventory = input.ManageInventory;
        service.Stamp(profile);
        await db.SaveChangesAsync(ct);
        service.Audit("AccessProfile", profile.Id, "SetPermissions");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        this.Success("FeedbackPermissionsSaved");
        return this.RedirectBack(nameof(Access));
    }
    [Authorize(Policy = "Access"), HttpGet]
    public async Task<IActionResult> Audit(int page = 1, CancellationToken ct = default)
    {
        var search = this.Search(); var query = db.Audit.AsNoTracking().Dates(this, clock);
        if (search != null) { var accounts = ListFiltering.Accounts(db, search); query = query.Where(x => x.EntityType.Contains(search) || x.Operation.Contains(search) || x.Reason.Contains(search) || accounts.Contains(x.UpdatedById)); }
        var entity = this.Value("entity"); if (entity.Length > 0) { query = query.Where(x => x.EntityType.Contains(entity)); }
        var operation = this.Value("operation"); if (operation.Length > 0) { query = query.Where(x => x.Operation.Contains(operation)); }
        this.Filters(new("entity", "ListEntityFilter"), new("operation", "ListOperationFilter"), new("from", "From", Date: true), new("to", "To", Date: true));
        var model = await reader.PageAsync(query.OrderByDescending(x => x.Id), this, search, null, null, ct);
        var ids = model.Items.Select(x => x.UpdatedById).ToArray();
        ViewBag.AccountNames = await shared.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.UserName ?? x.Id, ct);
        return View(model);
    }
}
