using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MedicApp.Data;
using MedicApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Trainify.Data;
namespace MedicApp.Tests;

[CollectionDefinition("RecurringSql", DisableParallelization = true)]
public class RecurringSqlGroup : ICollectionFixture<SqlFixture>;
[Collection("RecurringSql")]
public partial class RecurringCalendarTests(SqlFixture fixture)
{
    private IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Workforce"] = fixture.Connection }).Build();
    private Task<string> PrepareAsync() => new BookingSetup(Config(), SqlFixture.Clock()).PrepareAsync("doctor", "other", TestContext.Current.CancellationToken);
    private static IOptions<MedicalOptions> Settings(int minutes = 30, int days = 7) => Options.Create(new MedicalOptions { DemoBookingEnabled = true, DefaultSlotMinutes = minutes, BookingHorizonDays = days });
    private CalendarReader Reader(MedicDbContext db, TrainifyDbContext shared, int minutes = 30, int days = 7)
    {
        var clock = SqlFixture.Clock();
        var factory = new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance);
        return new(db, shared, new MedicalAccess(db, SqlFixture.Context("employee", fixture.EmployeeId)), clock, new ListReader(shared, clock), Settings(minutes, days), new StringLocalizer<UiText>(factory));
    }
    private MedicalService Service(MedicDbContext db, TrainifyDbContext shared, int minutes = 30, int days = 7) => new(db, shared, new MedicalAccess(db, SqlFixture.Context("employee", fixture.EmployeeId)), SqlFixture.Clock(), Settings(minutes, days));
    [Fact]
    public async Task Setup_RepeatDoesNotDuplicateAndGivesConfiguredAdministratorAccess()
    {
        await PrepareAsync();
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var appointments = await db.Appointments.CountAsync(TestContext.Current.CancellationToken);
        var people = await shared.People.CountAsync(TestContext.Current.CancellationToken);
        await PrepareAsync();
        Assert.Equal(appointments, await db.Appointments.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(people, await shared.People.CountAsync(TestContext.Current.CancellationToken));
        Assert.True(await db.AccessProfiles.AnyAsync(x => x.UserId == "other" && x.ManageAccess && x.ManageDoctors, TestContext.Current.CancellationToken));
        Assert.Equal(3, await shared.Users.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.LaborEvents.ToListAsync(TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Slots_AreThirtyMinuteRangesAndSkipSeededAppointments()
    {
        await PrepareAsync();
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var day = DateOnly.FromDateTime(SqlFixture.Clock().Local(DateTime.UtcNow).AddDays(1));
        var slots = await Reader(db, shared).SlotsAsync(day, day.AddDays(1), TestContext.Current.CancellationToken);
        Assert.NotEmpty(slots);
        Assert.All(slots, x => Assert.Equal(30, x.DurationMinutes));
        Assert.DoesNotContain(slots, x => x.Label == "08:00–08:30");
        Assert.Contains(slots, x => x.Label == "08:30–09:00");
    }
    [Fact]
    public async Task Recurrence_OffersDatesBeyondSeedWithoutWritingAndMaterializesOnlyOnReservation()
    {
        await PrepareAsync();
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var day = DateOnly.FromDateTime(SqlFixture.Clock().Local(DateTime.UtcNow).AddDays(20));
        var before = await db.Windows.CountAsync(TestContext.Current.CancellationToken);
        var slots = await Reader(db, shared, 30, 30).SlotsAsync(day, day.AddDays(1), TestContext.Current.CancellationToken);
        Assert.Equal(48, slots.Count);
        Assert.Equal(before, await db.Windows.CountAsync(TestContext.Current.CancellationToken));
        var id = await Service(db, shared, 30, 30).BookAtAsync(day.ToDateTime(new TimeOnly(8, 0)), Guid.NewGuid(), TestContext.Current.CancellationToken);
        var appointment = await db.Appointments.SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(30, (appointment.EndsAt - appointment.StartsAt).TotalMinutes);
        Assert.Equal(before + 1, await db.Windows.CountAsync(TestContext.Current.CancellationToken));
        var changed = await Reader(db, shared, 15, 30).SlotsAsync(day, day.AddDays(1), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(changed, x => x.Label == "08:15–08:30");
        Assert.Contains(changed, x => x.Label == "08:30–08:45");
        Assert.Equal(30, (appointment.EndsAt - appointment.StartsAt).TotalMinutes);
    }
    [Fact]
    public async Task SharedCalendar_OnlyShowsAnonymousOccupancyForOtherPeople()
    {
        await PrepareAsync();
        await using var db = fixture.Medical(); await using var shared = fixture.Shared();
        var day = DateOnly.FromDateTime(SqlFixture.Clock().Local(DateTime.UtcNow).AddDays(1));
        var events = await Reader(db, shared).EventsAsync(day, day.AddDays(1), TestContext.Current.CancellationToken);
        var busy = events.Where(x => x.ExtendedProps.Kind == "busy").ToList();
        Assert.True(busy.Count >= 3);
        Assert.All(busy, x =>
        {
            Assert.Null(x.ExtendedProps.AppointmentId); Assert.Empty(x.ExtendedProps.Employee);
            Assert.Empty(x.ExtendedProps.Doctor); Assert.Empty(x.ExtendedProps.Version);
            Assert.False(x.ExtendedProps.CanCancel); Assert.False(x.ExtendedProps.CanAttend);
            Assert.Equal("Ocupado · otra persona", x.Title);
        });
    }
    [Fact]
    public async Task Reservation_RejectsBeyondWeekAndConcurrentSameSlot()
    {
        await PrepareAsync();
        await using (var db = fixture.Medical())
        await using (var shared = fixture.Shared())
        {
            var beyond = SqlFixture.Clock().Local(DateTime.UtcNow.AddDays(8));
            var error = await Assert.ThrowsAsync<MedicalRuleException>(() => Service(db, shared).BookAtAsync(beyond, Guid.NewGuid(), TestContext.Current.CancellationToken));
            Assert.Equal("OutsideHorizon", error.Message);
        }
        var local = SqlFixture.Clock().Local(DateTime.UtcNow).Date.AddDays(1).AddHours(18);
        async Task<bool> Attempt(string user, int person)
        {
            await using var db = fixture.Medical(); await using var shared = fixture.Shared();
            try { await new MedicalService(db, shared, new MedicalAccess(db, SqlFixture.Context(user, person)), SqlFixture.Clock(), Settings()).BookAtAsync(local, Guid.NewGuid(), TestContext.Current.CancellationToken); return true; }
            catch (MedicalRuleException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Attempt("employee", fixture.EmployeeId), Attempt("other", fixture.OtherId)), x => x);
    }
    [Fact]
    public async Task HttpCalendar_EmployeeCanSelectRangeReserveAndCancelWithConfiguredDefaults()
    {
        await PrepareAsync();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Workforce"] = fixture.Connection,
                ["Medical:DataDirectory"] = Path.Combine(AppContext.BaseDirectory, "App_Data", Guid.NewGuid().ToString("N")),
                ["Medical:DemoBookingEnabled"] = "true",
                ["AllowedHosts"] = "localhost"
            }));
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        using var signed = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = "employee", ["Password"] = fixture.Password, ["__RequestVerificationToken"] = Token().Match(login).Groups[1].Value }));
        Assert.Equal(HttpStatusCode.Redirect, signed.StatusCode);
        var previewDay = DateOnly.FromDateTime(SqlFixture.Clock().Local(DateTime.UtcNow).AddDays(1)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var calendar = await client.GetStringAsync("/Appointments?date=" + previewDay);
        Assert.Contains("MODO DE PRUEBA", calendar);
        Assert.Contains("08:30–09:00", WebUtility.HtmlDecode(calendar));
        var day = DateOnly.FromDateTime(SqlFixture.Clock().Local(DateTime.UtcNow).AddDays(1)).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        using var times = JsonDocument.Parse(await client.GetStringAsync("/Appointments/Slots?date=" + day));
        Assert.Contains(times.RootElement.EnumerateArray(), x => x.GetProperty("label").GetString() == "08:30–09:00");
        var token = Token().Match(calendar).Groups[1].Value;
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        using var reserved = await client.PostAsync("/Appointments/Reserve", new FormUrlEncodedContent(new Dictionary<string, string> { ["Date"] = day, ["Time"] = "16:30", ["RequestKey"] = Guid.NewGuid().ToString(), ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.OK, reserved.StatusCode);
        using var result = JsonDocument.Parse(await reserved.Content.ReadAsStringAsync());
        var id = result.RootElement.GetProperty("id").GetInt32();
        await using var db = fixture.Medical();
        var row = await db.Appointments.SingleAsync(x => x.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(fixture.EmployeeId, row.PersonId);
        Assert.Equal(30, (row.EndsAt - row.StartsAt).TotalMinutes);
        using var cancelled = await client.PostAsync("/Appointments/Change", new FormUrlEncodedContent(new Dictionary<string, string> { ["Id"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture), ["Status"] = "3", ["Reason"] = "Prueba sintética", ["Version"] = Convert.ToBase64String(row.RowVersion), ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
    }
    [GeneratedRegex(@"name=\x22__RequestVerificationToken\x22[^>]*value=\x22([^\x22]+)\x22")]
    private static partial Regex Token();
}
