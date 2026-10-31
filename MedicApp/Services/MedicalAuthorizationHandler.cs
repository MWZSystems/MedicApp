using Microsoft.AspNetCore.Authorization;
namespace MedicApp.Services;

public record MedicalRequirement(string Key) : IAuthorizationRequirement;
public class MedicalAuthorizationHandler(MedicalAccess access, IHttpContextAccessor accessor) : AuthorizationHandler<MedicalRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, MedicalRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) { return; }
        var ct = accessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var allowed = requirement.Key switch
        {
            "LeaveEntry" => await access.DoctorIdAsync(ct) != null || await access.CanManageDoctorsAsync(ct) || await access.CanManageAccessAsync(ct),
            "ClinicalRecords" => await access.CanUseClinicalRecordsAsync(ct),
            "Inventory" => await access.CanUseInventoryAsync(ct),
            "Doctor" => await access.DoctorIdAsync(ct) != null,
            "Doctors" => await access.CanManageDoctorsAsync(ct),
            "Access" => await access.CanManageAccessAsync(ct),
            "Integration" => await access.CanReadIntegrationAsync(ct),
            _ => false
        };
        if (allowed) { context.Succeed(requirement); }
    }
}
