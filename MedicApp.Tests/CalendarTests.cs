using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Trainify.Data;
namespace MedicApp.Tests;

[Collection("Sql")]
public class CalendarTests(SqlFixture fixture)
{
    private static CalendarReader Reader(MedicDbContext db, TrainifyDbContext shared, string user, int person)
    {
        var clock = SqlFixture.Clock();
        var factory = new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance);
        return new(db, shared, new MedicalAccess(db, SqlFixture.Context(user, person)), clock, new ListReader(shared, clock), Options.Create(new MedicalOptions()), new StringLocalizer<UiText>(factory));
    }
    [Fact]
    public async Task AutoBooking_DateAndTime_UsesSessionPersonAndDoctor()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var service = SqlFixture.Service(db, shared, "employee", fixture.EmployeeId);
        var at = SqlFixture.Clock().Local(fixture.Tomorrow.AddHours(3));
        var key = Guid.NewGuid();
        var id = await service.BookAtAsync(at, key, ct);
        Assert.Equal(id, await service.BookAtAsync(at, key, ct));
        var row = await db.Appointments.SingleAsync(x => x.Id == id, ct);
        Assert.Equal(fixture.EmployeeId, row.PersonId);
        Assert.Equal(fixture.DoctorId, row.DoctorId);
        Assert.Equal(15, (row.EndsAt - row.StartsAt).TotalMinutes);
        Assert.True(await db.Audit.AnyAsync(x => x.EntityId == id && x.EntityType == "Appointment", ct));
    }
    [Fact]
    public async Task AutoBooking_SimultaneousRequests_DoesNotDoubleBook()
    {
        var at = SqlFixture.Clock().Local(fixture.Tomorrow.AddHours(3.5));
        async Task<bool> Attempt(string user, int person)
        {
            await using var db = fixture.Medical(); await using var shared = fixture.Shared();
            try { await SqlFixture.Service(db, shared, user, person).BookAtAsync(at, Guid.NewGuid(), TestContext.Current.CancellationToken); return true; }
            catch (MedicalRuleException) { return false; }
        }
        var results = await Task.WhenAll(Attempt("employee", fixture.EmployeeId), Attempt("other", fixture.OtherId));
        Assert.Single(results, x => x);
    }
    [Fact]
    public async Task AutoBooking_OutsideWindowOrUnaligned_Rejects()
    {
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var service = SqlFixture.Service(db, shared, "employee", fixture.EmployeeId);
        await Assert.ThrowsAsync<MedicalRuleException>(() => service.BookAtAsync(SqlFixture.Clock().Local(fixture.Tomorrow.AddMinutes(7)), Guid.NewGuid(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<MedicalRuleException>(() => service.BookAtAsync(SqlFixture.Clock().Local(fixture.Tomorrow.AddHours(9)), Guid.NewGuid(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<MedicalRuleException>(() => service.BookAtAsync(SqlFixture.Clock().Local(fixture.Tomorrow), Guid.Empty, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Slots_ReservedSlot_IsRemovedAndBusinessTimePreserved()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var at = SqlFixture.Clock().Local(fixture.Tomorrow.AddHours(2));
        var day = DateOnly.FromDateTime(at);
        var reader = Reader(db, shared, "employee", fixture.EmployeeId);
        var before = await reader.SlotsAsync(day, day.AddDays(1), ct);
        Assert.Contains(before, x => x.StartsAtLocal == at);
        Assert.Equal(before.Count, before.Select(x => x.StartsAtLocal).Distinct().Count());
        await SqlFixture.Service(db, shared, "employee", fixture.EmployeeId).BookAtAsync(at, Guid.NewGuid(), ct);
        Assert.DoesNotContain(await reader.SlotsAsync(day, day.AddDays(1), ct), x => x.StartsAtLocal == at);
    }
    [Fact]
    public async Task Events_EmployeeSeesOwnAndDoctorAssignedOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var at = SqlFixture.Clock().Local(fixture.Tomorrow.AddHours(4.5));
        var mine = await SqlFixture.Service(db, shared, "employee", fixture.EmployeeId).BookAtAsync(at, Guid.NewGuid(), ct);
        var other = await SqlFixture.Service(db, shared, "other", fixture.OtherId).BookAtAsync(at.AddMinutes(15), Guid.NewGuid(), ct);
        var day = DateOnly.FromDateTime(at);
        var employee = await Reader(db, shared, "employee", fixture.EmployeeId).EventsAsync(day, day.AddDays(1), ct);
        Assert.Contains(employee, x => x.ExtendedProps.AppointmentId == mine);
        Assert.DoesNotContain(employee, x => x.ExtendedProps.AppointmentId == other);
        Assert.DoesNotContain(employee, x => x.ExtendedProps.Employee.Contains("Otro ficticio", StringComparison.Ordinal));
        var doctor = await Reader(db, shared, "doctor", fixture.DoctorPersonId).EventsAsync(day, day.AddDays(1), ct);
        Assert.Contains(doctor, x => x.ExtendedProps.AppointmentId == mine);
        Assert.Contains(doctor, x => x.ExtendedProps.AppointmentId == other);
        var own = Assert.Single(employee, x => x.ExtendedProps.AppointmentId == mine);
        Assert.False(own.ExtendedProps.CanAttend);
        Assert.True(own.ExtendedProps.CanCancel);
        Assert.Equal(at.ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), own.Start);
    }
    [Fact]
    public async Task Slots_PastDay_ReturnsNoBookableTimes()
    {
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));
        Assert.Empty(await Reader(db, shared, "employee", fixture.EmployeeId).SlotsAsync(day, day.AddDays(1), TestContext.Current.CancellationToken));
    }
    [Fact]
    public void CalendarRange_TooLargeOrReversed_Rejects()
    {
        var from = new DateOnly(2026, 10, 1);
        Assert.Throws<MedicalRuleException>(() => CalendarReader.ValidateRange(from, from.AddDays(94)));
        Assert.Throws<MedicalRuleException>(() => CalendarReader.ValidateRange(from, from));
    }
}
