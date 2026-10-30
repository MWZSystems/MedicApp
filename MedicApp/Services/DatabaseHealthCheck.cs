using MedicApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace MedicApp.Services;

public class DatabaseHealthCheck(MedicDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var accessible = await db.Database.CanConnectAsync(cancellationToken);
            if (!accessible) { return HealthCheckResult.Unhealthy(); }
            await db.Doctors.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
            await db.LeaveDocuments.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Microsoft.Data.SqlClient.SqlException) { return HealthCheckResult.Unhealthy(); }
    }
}
