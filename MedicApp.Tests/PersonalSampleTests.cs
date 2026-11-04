using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Trainify.Domain;
namespace MedicApp.Tests;

public sealed class PersonalSampleFixture : IAsyncLifetime
{
    public SqlFixture Sql { get; } = new();
    public int DemoPersonId { get; private set; }
    public async ValueTask InitializeAsync()
    {
        await Sql.InitializeAsync(); await using var shared = Sql.Shared(); await using var db = Sql.Medical();
        var person = new Person { EmployeeNumber = DemoDataService.Prefix + "D1", FirstName = "DEMO Doctor General" }; shared.People.Add(person); await shared.SaveChangesAsync(); DemoPersonId = person.Id;
        db.Doctors.Add(new Doctor { PersonId = person.Id, ProfessionalLicense = DemoDataService.DoctorLicense, Specialty = "DEMO", UpdatedById = "doctor" }); await db.SaveChangesAsync();
    }
    public ValueTask DisposeAsync() => Sql.DisposeAsync();
}
[CollectionDefinition("PersonalSamplesSql", DisableParallelization = true)]
public class PersonalSampleGroup : ICollectionFixture<PersonalSampleFixture>;
[Collection("PersonalSamplesSql")]
public class PersonalSampleTests(PersonalSampleFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private PersonalSampleDataService Seeder() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Workforce"] = fixture.Sql.Connection }).Build(), SqlFixture.Clock());
    private static StringLocalizer<UiText> Text() => new StringLocalizer<UiText>(new ResourceManagerStringLocalizerFactory(Options.Create(new Microsoft.Extensions.Localization.LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance));
    [Fact]
    public async Task CreatesTwoMonthsWithoutLaborEventsAndIsIdempotent()
    {
        await using var before = fixture.Sql.Medical(); var events = await before.LaborEvents.CountAsync(Ct); await using var originalUsers = fixture.Sql.Shared(); var userCount = await originalUsers.Users.CountAsync(Ct);
        var first = await Seeder().CreateAsync("employee", "TEST-E1", "doctor", new DateOnly(2040, 11, 1), Ct);
        Assert.True(first.Created); Assert.Equal(12, first.Appointments); Assert.Equal(4, first.Leaves);
        await using var db = fixture.Sql.Medical(); var audit = await db.Audit.Where(x => x.Reason == first.Batch).ToListAsync(Ct);
        var ids = audit.Where(x => x.EntityType == "VisualSampleAppointment").Select(x => x.EntityId).ToArray(); var rows = await db.Appointments.Where(x => ids.Contains(x.Id)).ToListAsync(Ct);
        Assert.Equal(6, rows.Count(x => SqlFixture.Clock().Local(x.StartsAt).Month == 11)); Assert.Equal(6, rows.Count(x => SqlFixture.Clock().Local(x.StartsAt).Month == 12)); Assert.All(rows, x => Assert.Equal(TimeSpan.FromMinutes(30), x.EndsAt - x.StartsAt));
        Assert.Equal(4, await db.Leaves.CountAsync(x => x.Reference.StartsWith(first.Batch) && x.Status == LeaveStatus.Draft, Ct)); Assert.Equal(events, await db.LaborEvents.CountAsync(Ct));
        var windows = audit.Where(x => x.EntityType == "VisualSampleWindow").Select(x => x.EntityId).ToArray(); Assert.Equal(12, await db.Windows.CountAsync(x => windows.Contains(x.Id) && x.Generated, Ct));
        await using var shared = fixture.Sql.Shared(); Assert.Equal(userCount, await shared.Users.CountAsync(Ct));
        var again = await Seeder().CreateAsync("employee", "TEST-E1", "doctor", new DateOnly(2040, 11, 1), Ct); Assert.False(again.Created); Assert.Equal(first.Batch, again.Batch); Assert.Equal(12, again.Appointments); Assert.Equal(4, again.Leaves); Assert.Equal(audit.Count, await db.Audit.CountAsync(x => x.Reason == first.Batch, Ct));
    }
    [Fact]
    public async Task SamplesAreVisibleOnlyToAuthorizedReadersAndCannotBeApprovedOrUploaded()
    {
        var result = await Seeder().CreateAsync("employee", "TEST-E1", "doctor", new DateOnly(2041, 11, 1), Ct);
        await using var db = fixture.Sql.Medical(); await using var shared = fixture.Sql.Shared();
        var access = new MedicalAccess(db, SqlFixture.Context("employee", fixture.Sql.EmployeeId)); var clock = SqlFixture.Clock();
        var calendar = new LeaveCalendarReader(db, shared, access, clock, Text()); var events = await calendar.EventsAsync(fixture.Sql.EmployeeId, new DateOnly(2041, 11, 1), new DateOnly(2042, 1, 1), Ct);
        Assert.Equal(4, events.Count); Assert.All(events, x => { Assert.True(x.ExtendedProps.IsVisualSample); Assert.Contains("DEMO", x.Title); });
        var appointments = await new CalendarReader(db, shared, access, clock, new ListReader(shared, clock), Options.Create(new MedicalOptions()), Text()).EventsAsync(new DateOnly(2041, 11, 1), new DateOnly(2042, 1, 1), Ct); Assert.Equal(12, appointments.Count); Assert.All(appointments, x => Assert.Contains("DEMO", x.Title));
        var leave = await db.Leaves.FirstAsync(x => x.Reference.StartsWith(result.Batch), Ct);
        var service = SqlFixture.Service(db, shared, "doctor", fixture.DemoPersonId);
        var error = await Assert.ThrowsAsync<MedicalRuleException>(() => service.ChangeLeaveAsync(leave.Id, LeaveStatus.Approved, "Test approval", Convert.ToBase64String(leave.RowVersion), Ct)); Assert.Equal("VisualSampleReadOnly", error.Message);
        var admin = new LeaveRecordAccess(db, new MedicalAccess(db, SqlFixture.Context("doctor", fixture.Sql.DoctorPersonId))); Assert.False(await admin.CanUploadAsync(leave, Ct)); await Assert.ThrowsAsync<MedicalRuleException>(() => admin.GetAsync(leave.Id, true, Ct));
        Assert.False(await db.LaborEvents.AnyAsync(x => x.MedicalLeaveId == leave.Id, Ct)); await db.Entry(leave).ReloadAsync(Ct); Assert.Equal(LeaveStatus.Draft, leave.Status);
        var other = new LeaveCalendarReader(db, shared, new MedicalAccess(db, SqlFixture.Context("other", fixture.Sql.OtherId)), clock, Text()); await Assert.ThrowsAsync<MedicalRuleException>(() => other.EventsAsync(fixture.Sql.EmployeeId, new DateOnly(2041, 11, 1), new DateOnly(2042, 1, 1), Ct));
    }
    [Theory]
    [InlineData("employee", "WRONG", "doctor", "NotFound")]
    [InlineData("employee", "TEST-E1", "employee", "Forbidden")]
    public async Task RejectsMismatchedIdentitiesAndNonAdministratorsWithoutChanges(string user, string number, string actor, string expected)
    {
        await using var db = fixture.Sql.Medical(); var count = await db.Audit.CountAsync(Ct);
        var error = await Assert.ThrowsAsync<MedicalRuleException>(() => Seeder().CreateAsync(user, number, actor, new DateOnly(2043, 1, 1), Ct)); Assert.Equal(expected, error.Message); Assert.Equal(count, await db.Audit.CountAsync(Ct));
    }
    [Fact]
    public async Task NormalFormsCannotCreateReservedSampleReferences()
    {
        await using var db = fixture.Sql.Medical(); await using var shared = fixture.Sql.Shared(); var service = SqlFixture.Service(db, shared, "doctor", fixture.Sql.DoctorPersonId);
        var error = await Assert.ThrowsAsync<MedicalRuleException>(() => service.CreateLeaveAsync(fixture.Sql.EmployeeId, new DateTime(2044, 1, 1), new DateTime(2044, 1, 3), PersonalSampleDataService.Prefix.ToLowerInvariant() + "test", null, Ct)); Assert.Equal("VisualSampleReadOnly", error.Message);
    }
}
