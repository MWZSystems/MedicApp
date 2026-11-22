using System.Text.RegularExpressions;
using MedicApp.Data;
using MedicApp.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Trainify.Data;
using WFMCore.Medical;

namespace MedicApp.Tests;

[Collection("Sql")]
public partial class DeliveryTests(SqlFixture fixture)
{
    [Fact]
    public async Task Reader_AuthorizationAndCancellation_ReflectsCurrentRestriction()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.Medical();
        await using var shared = fixture.Shared();
        var service = SqlFixture.Service(db, shared, "doctor", fixture.DoctorPersonId);
        var start = fixture.Tomorrow.AddDays(10);
        var id = await service.CreateLeaveAsync(fixture.EmployeeId, SqlFixture.Clock().Local(start), SqlFixture.Clock().Local(start.AddHours(1)), "TEST-" + Guid.NewGuid().ToString("N"), null, ct);
        var row = await db.Leaves.SingleAsync(x => x.Id == id, ct);
        var reader = new MedicalRestrictionReader(shared);
        Assert.Empty(await reader.FindAsync(fixture.EmployeeId, start, start.AddHours(1), ct));
        await service.ChangeLeaveAsync(id, LeaveStatus.Approved, "Test only", Convert.ToBase64String(row.RowVersion), ct);
        var restriction = Assert.Single(await reader.FindAsync(fixture.EmployeeId, start, start.AddHours(1), ct));
        Assert.Equal(id, restriction.LeaveId);
        Assert.Equal(DateTimeKind.Utc, restriction.StartsAtUtc.Kind);
        Assert.Empty(await reader.FindAsync(fixture.EmployeeId, start.AddHours(1), start.AddHours(2), ct));
        await service.ChangeLeaveAsync(id, LeaveStatus.Cancelled, "Test only", Convert.ToBase64String(row.RowVersion), ct);
        Assert.Empty(await reader.FindAsync(fixture.EmployeeId, start, start.AddHours(1), ct));
    }

    [Fact]
    public async Task Installer_RunTwice_OnlyCreatesMedicalTables()
    {
        var ct = TestContext.Current.CancellationToken;
        var target = new SqlConnectionStringBuilder(fixture.Connection) { InitialCatalog = "MedicApp_Test_Script_" + Guid.NewGuid().ToString("N") };
        await using var shared = new TrainifyDbContext(new DbContextOptionsBuilder<TrainifyDbContext>().UseSqlServer(target.ConnectionString).Options);
        await shared.Database.EnsureCreatedAsync(ct);
        try
        {
            var file = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../database/01_InstallMedicApp.sql"));
            var script = await File.ReadAllTextAsync(file, ct);
            script = string.Join(Environment.NewLine, script.Split('\n').Where(line => !line.TrimStart().StartsWith(':')));
            await using var connection = new SqlConnection(target.ConnectionString);
            await connection.OpenAsync(ct);
            foreach (var pass in Enumerable.Range(0, 2))
            {
                foreach (var batch in BatchSeparator().Split(script).Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = batch;
                    command.CommandTimeout = 120;
                    await command.ExecuteNonQueryAsync(ct);
                }
            }
            await using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM sys.tables AS T WHERE SCHEMA_NAME(T.schema_id) = N'medic' AND T.name <> N'__EFMigrationsHistory';";
            Assert.Equal(12, Convert.ToInt32(await count.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(0, await shared.Users.CountAsync(ct));
            Assert.Equal(0, await shared.People.CountAsync(ct));
        }
        finally
        {
            await shared.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task SchemabindingScript_RunTwice_KeepsViewContractAndReader()
    {
        var ct = TestContext.Current.CancellationToken;
        var target = new SqlConnectionStringBuilder(fixture.Connection) { InitialCatalog = "MedicApp_Test_Script_" + Guid.NewGuid().ToString("N") };
        await using var shared = new TrainifyDbContext(new DbContextOptionsBuilder<TrainifyDbContext>().UseSqlServer(target.ConnectionString).Options);
        await shared.Database.EnsureCreatedAsync(ct);
        try
        {
            await using var connection = new SqlConnection(target.ConnectionString);
            await connection.OpenAsync(ct);
            await RunScriptAsync(connection, "01_InstallMedicApp.sql", ct);
            await RunScriptAsync(connection, "02_RuntimeRoles.sql", ct);
            const string Contract = """
                SELECT STRING_AGG(CONCAT(OBJECT_NAME(C.object_id), '.', C.name, ':', TYPE_NAME(C.user_type_id), '(', C.max_length, ',', C.is_nullable, ')'), '|') WITHIN GROUP (ORDER BY C.object_id, C.column_id)
                FROM sys.columns AS C
                WHERE C.object_id IN (OBJECT_ID(N'medic.vw_ApprovedLaborRestrictions'), OBJECT_ID(N'medic.vw_LaborEvents'));
                """;
            const string Bound = "SELECT COUNT(*) FROM sys.sql_modules AS M WHERE M.is_schema_bound = 1 AND M.object_id IN (OBJECT_ID(N'medic.vw_ApprovedLaborRestrictions'), OBJECT_ID(N'medic.vw_LaborEvents'));";
            const string Grants = "SELECT COUNT(*) FROM sys.database_permissions AS P WHERE P.grantee_principal_id = DATABASE_PRINCIPAL_ID(N'WfmMedicalReader') AND P.permission_name = N'SELECT' AND P.state = 'G';";
            var before = await ScalarAsync(connection, Contract, ct);
            Assert.Equal(0, Convert.ToInt32(await ScalarAsync(connection, Bound, ct), System.Globalization.CultureInfo.InvariantCulture));

            // Re-runnable: the second pass must leave the same result.
            await RunScriptAsync(connection, "03_SchemabindingViews.sql", ct);
            await RunScriptAsync(connection, "03_SchemabindingViews.sql", ct);

            Assert.Equal(before, await ScalarAsync(connection, Contract, ct));
            Assert.Equal(2, Convert.ToInt32(await ScalarAsync(connection, Bound, ct), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(2, Convert.ToInt32(await ScalarAsync(connection, Grants, ct), System.Globalization.CultureInfo.InvariantCulture));
            // The WFMCore reader and the event cursor still read the views.
            Assert.Empty(await new MedicalRestrictionReader(shared).FindAsync(1, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), ct));
            Assert.Equal(0, Convert.ToInt32(await ScalarAsync(connection, "SELECT COUNT(*) FROM medic.vw_LaborEvents AS LE WHERE LE.PKLaborEvent > 0;", ct), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            await shared.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    private static async Task RunScriptAsync(SqlConnection connection, string name, CancellationToken ct)
    {
        var file = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../database", name));
        var script = await File.ReadAllTextAsync(file, ct);
        script = string.Join(Environment.NewLine, script.Split('\n').Where(line => !line.TrimStart().StartsWith(':')));
        foreach (var batch in BatchSeparator().Split(script).Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<object?> ScalarAsync(SqlConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(ct);
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex BatchSeparator();
}
