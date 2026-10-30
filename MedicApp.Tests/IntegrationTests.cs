using MedicApp.Models.Entities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MedicApp.Services;
using Microsoft.EntityFrameworkCore;
namespace MedicApp.Tests;

[Collection("Sql")]
public class IntegrationTests(SqlFixture fixture)
{
    [Fact]
    public async Task Booking_DuplicateRequest_IsIdempotentAndAudited()
    {
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var service = SqlFixture.Service(db, shared, "employee", fixture.EmployeeId);
        var key = Guid.NewGuid();
        var window = await db.Windows.SingleAsync();
        var start = SqlFixture.Clock().Local(fixture.Tomorrow);
        var id = await service.BookAsync(window.Id, start, key, TestContext.Current.CancellationToken);
        Assert.Equal(id, await service.BookAsync(window.Id, start, key, TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.Appointments.CountAsync(x => x.RequestKey == key));
        Assert.True(await db.Audit.AnyAsync(x => x.EntityType == "Appointment" && x.EntityId == id && x.Operation == "Book"));
    }
    [Fact]
    public async Task Booking_SameSlotConcurrent_CreatesAtMostOne()
    {
        var at = fixture.Tomorrow.AddMinutes(30);
        async Task<bool> Attempt(string user, int person)
        {
            await using var db = fixture.Medical(); await using var shared = fixture.Shared();
            var service = SqlFixture.Service(db, shared, user, person);
            var window = await db.Windows.SingleAsync();
            try { await service.BookAsync(window.Id, SqlFixture.Clock().Local(at), Guid.NewGuid(), TestContext.Current.CancellationToken); return true; }
            catch (MedicalRuleException) { return false; }
            catch (Microsoft.Data.SqlClient.SqlException e) when (e.Number == 1205) { return false; }
            catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 1205 }) { return false; }
        }
        var results = await Task.WhenAll(Attempt("employee", fixture.EmployeeId), Attempt("other", fixture.OtherId));
        Assert.Single(results, x => x);
        await using var verify = fixture.Medical();
        Assert.Equal(1, await verify.Appointments.CountAsync(x => x.StartsAt == at && x.Status == AppointmentStatus.Scheduled));
    }
    [Fact]
    public async Task Booking_OtherEmployeeCannotCancel_Rejects()
    {
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var employee = SqlFixture.Service(db, shared, "employee", fixture.EmployeeId);
        var window = await db.Windows.SingleAsync();
        var id = await employee.BookAsync(window.Id, SqlFixture.Clock().Local(fixture.Tomorrow.AddHours(1)), Guid.NewGuid(), TestContext.Current.CancellationToken);
        var row = await db.Appointments.SingleAsync(x => x.Id == id);
        var other = SqlFixture.Service(db, shared, "other", fixture.OtherId);
        await Assert.ThrowsAsync<MedicalRuleException>(() => other.ChangeAppointmentAsync(id, AppointmentStatus.Cancelled, "Test", Convert.ToBase64String(row.RowVersion), TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Leave_ApproveAndCancel_EmitsOrderedLaborRevisions()
    {
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var service = SqlFixture.Service(db, shared, "doctor", fixture.DoctorPersonId);
        var start = SqlFixture.Clock().Local(fixture.Tomorrow.AddDays(1));
        var id = await service.CreateLeaveAsync(fixture.EmployeeId, start, start.AddHours(8), "TEST-" + Guid.NewGuid().ToString("N"), null, TestContext.Current.CancellationToken);
        var row = await db.Leaves.SingleAsync(x => x.Id == id);
        Assert.False(await db.LaborEvents.AnyAsync(x => x.MedicalLeaveId == id));
        await service.ChangeLeaveAsync(id, LeaveStatus.Approved, "Autorización ficticia", Convert.ToBase64String(row.RowVersion), TestContext.Current.CancellationToken);
        await service.ChangeLeaveAsync(id, LeaveStatus.Cancelled, "Cancelación ficticia", Convert.ToBase64String(row.RowVersion), TestContext.Current.CancellationToken);
        var events = await db.LaborEvents.Where(x => x.MedicalLeaveId == id).OrderBy(x => x.Id).ToListAsync();
        Assert.Equal((int[])[2, 3], events.Select(x => x.Revision));
        Assert.Equal(new[] { LeaveStatus.Approved, LeaveStatus.Cancelled }, events.Select(x => x.Status));
    }
    [Fact]
    public async Task Leave_EmployeeCannotIssue_Rejects()
    {
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var service = SqlFixture.Service(db, shared, "employee", fixture.EmployeeId);
        await Assert.ThrowsAsync<MedicalRuleException>(() => service.CreateLeaveAsync(fixture.OtherId, DateTime.Today, DateTime.Today.AddDays(1), "TEST", null, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Leave_DoctorCannotIssueForSelf_Rejects()
    {
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var service = SqlFixture.Service(db, shared, "doctor", fixture.DoctorPersonId);
        await Assert.ThrowsAsync<MedicalRuleException>(() => service.CreateLeaveAsync(fixture.DoctorPersonId, DateTime.Today, DateTime.Today.AddDays(1), "TEST", null, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Leave_OverlappingApproved_RejectsAndPreservesDraft()
    {
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var service = SqlFixture.Service(db, shared, "doctor", fixture.DoctorPersonId);
        var start = SqlFixture.Clock().Local(fixture.Tomorrow.AddDays(3));
        var first = await service.CreateLeaveAsync(fixture.OtherId, start, start.AddHours(8), "TEST-" + Guid.NewGuid().ToString("N"), null, TestContext.Current.CancellationToken);
        var row = await db.Leaves.SingleAsync(x => x.Id == first);
        await service.ChangeLeaveAsync(first, LeaveStatus.Approved, "Ficticio", Convert.ToBase64String(row.RowVersion), TestContext.Current.CancellationToken);
        var second = await service.CreateLeaveAsync(fixture.OtherId, start.AddHours(1), start.AddHours(2), "TEST-" + Guid.NewGuid().ToString("N"), null, TestContext.Current.CancellationToken);
        var secondRow = await db.Leaves.SingleAsync(x => x.Id == second);
        await Assert.ThrowsAsync<MedicalRuleException>(() => service.ChangeLeaveAsync(second, LeaveStatus.Approved, "Ficticio", Convert.ToBase64String(secondRow.RowVersion), TestContext.Current.CancellationToken));
        await using var verify = fixture.Medical();
        Assert.Equal(LeaveStatus.Draft, (await verify.Leaves.SingleAsync(x => x.Id == second)).Status);
        Assert.False(await verify.LaborEvents.AnyAsync(x => x.MedicalLeaveId == second));
    }
    [Fact]
    public async Task Leave_StaleVersion_RollsBackAuditAndEvent()
    {
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var service = SqlFixture.Service(db, shared, "doctor", fixture.DoctorPersonId);
        var start = SqlFixture.Clock().Local(fixture.Tomorrow.AddDays(5));
        var id = await service.CreateLeaveAsync(fixture.EmployeeId, start, start.AddHours(2), "TEST-" + Guid.NewGuid().ToString("N"), null, TestContext.Current.CancellationToken);
        var row = await db.Leaves.SingleAsync(x => x.Id == id);
        var version = Convert.ToBase64String(row.RowVersion);
        await service.ChangeLeaveAsync(id, LeaveStatus.Approved, "Ficticio", version, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.ChangeLeaveAsync(id, LeaveStatus.Cancelled, "Ficticio", version, TestContext.Current.CancellationToken));
        await using var verify = fixture.Medical();
        Assert.Equal(LeaveStatus.Approved, (await verify.Leaves.SingleAsync(x => x.Id == id)).Status);
        Assert.Equal(1, await verify.LaborEvents.CountAsync(x => x.MedicalLeaveId == id));
        Assert.False(await verify.Audit.AnyAsync(x => x.EntityType == "MedicalLeave" && x.EntityId == id && x.Operation == "Cancelled"));
    }
    [Fact]
    public async Task Migration_Reapply_IsNoOpAndDoesNotOwnSharedTables()
    {
        await using var db = fixture.Medical();
        var before = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        await db.Database.MigrateAsync();
        Assert.Equal(before, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        var sharedEntities = db.GetService<IDesignTimeModel>().Model.GetEntityTypes().Where(x => x.GetSchema() == "dbo");
        Assert.All(sharedEntities, x => Assert.True(x.IsTableExcludedFromMigrations()));
    }
}

