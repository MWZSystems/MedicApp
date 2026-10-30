using System.ComponentModel.DataAnnotations;
using MedicApp.Models.Entities;
using MedicApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
namespace MedicApp.Tests;

[Collection("Sql")]
public class ConfigurationTests(SqlFixture fixture)
{
    [Fact]
    public void DefaultHours_AllowMidnightAndAllDay()
    {
        var settings = new MedicalOptions();
        Assert.True(settings.Open24Hours);
        Assert.True(settings.Allows(new DateTime(2026, 11, 1), new DateTime(2026, 11, 2)));
    }
    [Theory]
    [InlineData(7, 45, 8, 0, false)]
    [InlineData(8, 0, 8, 15, true)]
    [InlineData(17, 45, 18, 0, true)]
    [InlineData(17, 50, 18, 5, false)]
    public void DayHours_RequireWholeAppointmentInside(int sh, int sm, int eh, int em, bool allowed)
    {
        var settings = new MedicalOptions { Open24Hours = false };
        Assert.Equal(allowed, settings.Allows(new DateTime(2026, 11, 1, sh, sm, 0), new DateTime(2026, 11, 1, eh, em, 0)));
    }
    [Fact]
    public void OvernightHours_AllowCrossingMidnightAndRejectDaytime()
    {
        var settings = new MedicalOptions { Open24Hours = false, OpeningTime = "22:00", ClosingTime = "06:00" };
        Assert.True(settings.Allows(new DateTime(2026, 11, 1, 23, 45, 0), new DateTime(2026, 11, 2, 0, 15, 0)));
        Assert.True(settings.Allows(new DateTime(2026, 11, 2, 5, 45, 0), new DateTime(2026, 11, 2, 6, 0, 0)));
        Assert.False(settings.Allows(new DateTime(2026, 11, 2, 12, 0, 0), new DateTime(2026, 11, 2, 12, 15, 0)));
    }
    [Fact]
    public void Configuration_RejectsInvalidHorizonDurationAndEqualHours()
    {
        var settings = new MedicalOptions { BookingHorizonDays = 0, DefaultSlotMinutes = 121 };
        Assert.False(Validator.TryValidateObject(settings, new ValidationContext(settings), [], true));
        settings = new MedicalOptions { Open24Hours = false, OpeningTime = "08:00", ClosingTime = "08:00" };
        Assert.False(Validator.TryValidateObject(settings, new ValidationContext(settings), [], true));
    }
    [Fact]
    public async Task Booking_RejectsConfiguredHoursAndHorizonEvenWithWindowId()
    {
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var at = SqlFixture.Clock().Local(fixture.Tomorrow);
        var opening = at.AddHours(1).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var closing = at.AddHours(2).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var options = Options.Create(new MedicalOptions { Open24Hours = false, OpeningTime = opening, ClosingTime = closing });
        var service = new MedicalService(db, shared, new MedicalAccess(db, SqlFixture.Context("employee", fixture.EmployeeId)), SqlFixture.Clock(), options);
        var window = await db.Windows.FirstAsync(x => x.DoctorId == fixture.DoctorId, TestContext.Current.CancellationToken);
        var error = await Assert.ThrowsAsync<MedicalRuleException>(() => service.BookAsync(window.Id, at, Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.Equal("OutsideServiceHours", error.Message);
        service = new MedicalService(db, shared, new MedicalAccess(db, SqlFixture.Context("employee", fixture.EmployeeId)), SqlFixture.Clock(), Options.Create(new MedicalOptions { BookingHorizonDays = 1 }));
        error = await Assert.ThrowsAsync<MedicalRuleException>(() => service.BookAtAsync(at, Guid.NewGuid(), TestContext.Current.CancellationToken));
        Assert.Equal("OutsideHorizon", error.Message);
    }
    [Fact]
    public async Task DemoGeneration_IsIdempotentAuditedAndIsolatedFromRealBookings()
    {
        await using var isolated = new SqlFixture();
        await isolated.InitializeAsync();
        var ct = TestContext.Current.CancellationToken;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Workforce"] = isolated.Connection }).Build();
        var demo = new DemoDataService(config, SqlFixture.Clock(), Options.Create(new MedicalOptions()));
        await using var db = isolated.Medical(); await using var shared = isolated.Shared();
        var usersBefore = await shared.Users.CountAsync(ct);
        var peopleBefore = await shared.People.CountAsync(ct);
        var laborBefore = await db.LaborEvents.CountAsync(ct);
        await demo.CreateAsync("doctor", ct);
        await demo.CreateAsync("doctor", ct);
        Assert.Equal(usersBefore, await shared.Users.CountAsync(ct));
        Assert.Equal(peopleBefore + 4, await shared.People.CountAsync(ct));
        Assert.Equal(laborBefore, await db.LaborEvents.CountAsync(ct));
        Assert.Equal(4, await db.Audit.CountAsync(x => x.EntityType == "DemoBatch", ct));
        var demoPerson = await shared.People.SingleAsync(x => x.EmployeeNumber == DemoDataService.Prefix + "D1", ct);
        var demoDoctor = await db.Doctors.SingleAsync(x => x.PersonId == demoPerson.Id, ct);
        Assert.Equal(33, await db.Appointments.CountAsync(x => x.DoctorId == demoDoctor.Id, ct));
        var day = DateOnly.FromDateTime(SqlFixture.Clock().Local(DateTime.UtcNow).AddDays(1));
        Assert.NotEmpty(await demo.EventsAsync(day, day.AddDays(1), ct));
        var window = await db.Windows.FirstAsync(x => x.DoctorId == demoDoctor.Id && x.StartsAt > DateTime.UtcNow, ct);
        var error = await Assert.ThrowsAsync<MedicalRuleException>(() => SqlFixture.Service(db, shared, "employee", isolated.EmployeeId).BookAsync(window.Id, SqlFixture.Clock().Local(window.StartsAt.AddMinutes(15)), Guid.NewGuid(), ct));
        Assert.Equal("UnavailableSlot", error.Message);
        error = await Assert.ThrowsAsync<MedicalRuleException>(() => SqlFixture.Service(db, shared, "doctor", isolated.DoctorPersonId).CreateLeaveAsync(demoPerson.Id, SqlFixture.Clock().Local(window.StartsAt), SqlFixture.Clock().Local(window.EndsAt), "DEMO", null, ct));
        Assert.Equal("DemoLeaveDisabled", error.Message);
    }
    [Fact]
    public async Task DemoGeneration_WithoutAdmin_RollsBack()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Workforce"] = fixture.Connection }).Build();
        var error = await Assert.ThrowsAsync<MedicalRuleException>(() => new DemoDataService(config, SqlFixture.Clock(), Options.Create(new MedicalOptions())).CreateAsync("employee", TestContext.Current.CancellationToken));
        Assert.Equal("Forbidden", error.Message);
    }
}
