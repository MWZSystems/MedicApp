using MedicApp.Data;
using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore;
namespace MedicApp.Services;

public class LeaveRecordAccess(MedicDbContext db, MedicalAccess access)
{
    public Task<bool> IsAdministratorAsync(CancellationToken ct) => AdministratorAsync(ct);
    private async Task<bool> AdministratorAsync(CancellationToken ct) => await access.CanManageDoctorsAsync(ct) || await access.CanManageAccessAsync(ct);
    public async Task<bool> CanRegisterAsync(CancellationToken ct) => await access.DoctorIdAsync(ct) != null || await AdministratorAsync(ct);
    public async Task<bool> CanUploadAsync(MedicalLeave leave, CancellationToken ct) => !PersonalSampleDataService.IsSample(leave.Reference) && (await AdministratorAsync(ct) || await access.DoctorIdAsync(ct) == leave.DoctorId);
    public async Task<MedicalLeave> GetAsync(int id, bool write, CancellationToken ct)
    {
        var leave = await db.Leaves.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Available, ct) ?? throw new MedicalRuleException("NotFound");
        if (await AdministratorAsync(ct) || await access.DoctorIdAsync(ct) == leave.DoctorId || (!write && leave.PersonId == access.PersonId)) { if (write && PersonalSampleDataService.IsSample(leave.Reference)) { throw new MedicalRuleException("VisualSampleReadOnly"); } return leave; }
        throw new MedicalRuleException("Forbidden");
    }
    public async Task<IQueryable<MedicalLeave>> PersonQueryAsync(int personId, CancellationToken ct)
    {
        var administrator = await AdministratorAsync(ct);
        var doctor = await access.DoctorIdAsync(ct);
        if (!administrator && doctor == null && personId != access.PersonId) { throw new MedicalRuleException("Forbidden"); }
        var query = db.Leaves.AsNoTracking().Where(x => x.Available && x.PersonId == personId);
        if (!administrator) { query = query.Where(x => x.PersonId == access.PersonId || x.DoctorId == doctor); }
        return query;
    }
}
