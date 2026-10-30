using MedicApp.Data;
using Microsoft.EntityFrameworkCore;
namespace MedicApp.Services;

public static class MedicalLocks
{
    /// <summary>Serializes short reservation/event transactions to preserve capacity and commit-ordered cursors.</summary>
    public static Task AcquireAsync(MedicDbContext db, string resource, CancellationToken ct) => db.Database.ExecuteSqlAsync($"DECLARE @Result INT; EXEC @Result = sys.sp_getapplock @Resource = {resource}, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 10000; IF @Result < 0 THROW 50010, 'Medical transaction lock timeout', 1;", ct);
}
