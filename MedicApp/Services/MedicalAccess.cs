using System.Security.Claims;
using MedicApp.Data;
using Microsoft.EntityFrameworkCore;
namespace MedicApp.Services;

public class MedicalAccess(MedicDbContext db, IHttpContextAccessor accessor)
{
    public string UserId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new MedicalRuleException("Forbidden");
    public int PersonId => int.TryParse(accessor.HttpContext?.User.FindFirstValue("personId"), out var id) ? id : throw new MedicalRuleException("Forbidden");
    public Task<int?> DoctorIdAsync(CancellationToken ct) => db.Doctors.AsNoTracking().Where(x => x.PersonId == PersonId && x.Available).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct);
    public Task<bool> CanManageDoctorsAsync(CancellationToken ct) => db.AccessProfiles.AsNoTracking().AnyAsync(x => x.UserId == UserId && x.Available && x.ManageDoctors, ct);
    public Task<bool> CanManageAccessAsync(CancellationToken ct) => db.AccessProfiles.AsNoTracking().AnyAsync(x => x.UserId == UserId && x.Available && x.ManageAccess, ct);
    public async Task<bool> CanUseClinicalRecordsAsync(CancellationToken ct) => await DoctorIdAsync(ct) != null || await db.AccessProfiles.AsNoTracking().AnyAsync(x => x.UserId == UserId && x.Available && (x.ManageClinicalRecords || x.ManageAccess), ct);
    public async Task<bool> CanUseInventoryAsync(CancellationToken ct) => await DoctorIdAsync(ct) != null || await db.AccessProfiles.AsNoTracking().AnyAsync(x => x.UserId == UserId && x.Available && (x.ManageInventory || x.ManageAccess), ct);
    public Task<bool> CanReadIntegrationAsync(CancellationToken ct) => db.AccessProfiles.AsNoTracking().AnyAsync(x => x.UserId == UserId && x.Available && x.ReadIntegration, ct);
}
