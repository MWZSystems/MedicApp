using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Trainify.Domain;
namespace MedicApp.Tests;

public sealed class LeaveCalendarFixture : IAsyncLifetime
{
    public SqlFixture Sql { get; } = new();
    public int HistoricalPerson { get; private set; }
    public async ValueTask InitializeAsync()
    {
        await Sql.InitializeAsync();
        await using var shared = Sql.Shared();
        var person = new Person { EmployeeNumber = "ARCH-999", FirstName = "Laura", LastName = "Histórica", Active = false };
        shared.People.Add(person); await shared.SaveChangesAsync(); HistoricalPerson = person.Id;
        await using var db = Sql.Medical();
        var clock = SqlFixture.Clock();
        MedicalLeave Leave(int patient, DateTime start, DateTime end, LeaveStatus state = LeaveStatus.Approved, bool available = true) => new()
        {
            PersonId = patient,
            DoctorId = Sql.DoctorId,
            StartsAt = clock.ToUtc(start),
            EndsAt = clock.ToUtc(end),
            Status = state,
            Reference = "SYNTHETIC-REFERENCE-" + Guid.NewGuid().ToString("N"),
            CancellationReason = "SYNTHETIC-PRIVATE-REASON",
            Available = available,
            UpdatedById = "doctor"
        };
        db.Leaves.AddRange(
            Leave(Sql.EmployeeId, new DateTime(2027, 2, 25), new DateTime(2027, 3, 3)),
            Leave(Sql.EmployeeId, new DateTime(2027, 3, 8, 10, 0, 0), new DateTime(2027, 3, 10, 14, 0, 0)),
            Leave(Sql.EmployeeId, new DateTime(2027, 3, 15), new DateTime(2027, 3, 18), LeaveStatus.Draft),
            Leave(Sql.EmployeeId, new DateTime(2027, 3, 20), new DateTime(2027, 3, 23), LeaveStatus.Cancelled),
            Leave(Sql.EmployeeId, new DateTime(2027, 3, 25), new DateTime(2027, 3, 28), available: false),
            Leave(Sql.OtherId, new DateTime(2027, 3, 1), new DateTime(2027, 3, 5)),
            Leave(person.Id, new DateTime(2025, 1, 15), new DateTime(2025, 1, 20))
        );
        await db.SaveChangesAsync();
    }
    public ValueTask DisposeAsync() => Sql.DisposeAsync();
}
[CollectionDefinition("LeaveCalendarSql", DisableParallelization = true)]
public class LeaveCalendarGroup : ICollectionFixture<LeaveCalendarFixture>;
[Collection("LeaveCalendarSql")]
public partial class LeaveCalendarTests(LeaveCalendarFixture fixture) : IAsyncDisposable
{
    private readonly List<Trainify.Data.TrainifyDbContext> sharedContexts = [];
    public async ValueTask DisposeAsync() { foreach (var shared in sharedContexts) { await shared.DisposeAsync(); } GC.SuppressFinalize(this); }
    private sealed class FixedTime(DateTime utc) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(utc); }
    private LeaveCalendarReader Reader(MedicDbContext db, string user = "employee", int? person = null, DateTime? localNow = null)
    {
        var clock = new MedicalClock(Options.Create(new MedicalOptions()), new FixedTime(SqlFixture.Clock().ToUtc(localNow ?? new DateTime(2027, 2, 27, 12, 0, 0))));
        var factory = new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions { ResourcesPath = "Resources" }), NullLoggerFactory.Instance);
        var shared = fixture.Sql.Shared(); sharedContexts.Add(shared);
        return new(db, shared, new MedicalAccess(db, SqlFixture.Context(user, person ?? fixture.Sql.EmployeeId)), clock, new StringLocalizer<UiText>(factory));
    }
    [Fact]
    public async Task MidnightEnd_UsesExclusiveEndWithoutHighlightingExtraDay()
    {
        await using var db = fixture.Sql.Medical();
        var rows = await Reader(db).EventsAsync(fixture.Sql.EmployeeId, new DateOnly(2027, 2, 1), new DateOnly(2027, 3, 5), TestContext.Current.CancellationToken);
        var row = Assert.Single(rows);
        Assert.True(row.AllDay); Assert.Equal("2027-02-25", row.Start); Assert.Equal("2027-03-03", row.End);
        Assert.Equal("2027-03-02", row.ExtendedProps.CoveredThrough);
        Assert.Equal("2027-03-03T00:00:00", row.ExtendedProps.EndsAtLocal);
    }
    [Fact]
    public async Task TimedEnd_HighlightsLastPartialDayAndPreservesExactTime()
    {
        await using var db = fixture.Sql.Medical();
        var rows = await Reader(db).EventsAsync(fixture.Sql.EmployeeId, new DateOnly(2027, 3, 8), new DateOnly(2027, 3, 12), TestContext.Current.CancellationToken);
        var row = Assert.Single(rows);
        Assert.Equal("2027-03-08", row.Start); Assert.Equal("2027-03-11", row.End);
        Assert.Equal("2027-03-10", row.ExtendedProps.CoveredThrough);
        Assert.Equal("2027-03-10T14:00:00", row.ExtendedProps.EndsAtLocal);
    }
    [Fact]
    public async Task Feed_OnlyApprovedAvailablePeriodsAndIntervalsOverlappingVisibleRange()
    {
        await using var db = fixture.Sql.Medical(); var reader = Reader(db);
        var rows = await reader.EventsAsync(fixture.Sql.EmployeeId, new DateOnly(2027, 3, 1), new DateOnly(2027, 4, 1), TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Count);
        Assert.Single(await reader.EventsAsync(fixture.Sql.EmployeeId, new DateOnly(2027, 3, 3), new DateOnly(2027, 3, 11), TestContext.Current.CancellationToken));
        var json = JsonSerializer.Serialize(rows);
        Assert.DoesNotContain("SYNTHETIC-REFERENCE", json); Assert.DoesNotContain("SYNTHETIC-PRIVATE-REASON", json);
        Assert.DoesNotContain("RowVersion", json); Assert.DoesNotContain("DoctorId", json);
    }
    [Fact]
    public async Task Employee_CannotSelectOtherPersonOrSearchDirectory()
    {
        await using var db = fixture.Sql.Medical(); var reader = Reader(db);
        var error = await Assert.ThrowsAsync<MedicalRuleException>(() => reader.EventsAsync(fixture.Sql.OtherId, new DateOnly(2027, 3, 1), new DateOnly(2027, 4, 1), TestContext.Current.CancellationToken));
        Assert.Equal("Forbidden", error.Message);
        await Assert.ThrowsAsync<MedicalRuleException>(() => reader.PageAsync(fixture.Sql.OtherId, TestContext.Current.CancellationToken));
        Assert.Empty(await reader.SearchAsync("TEST-E2", TestContext.Current.CancellationToken));
        Assert.Single(await reader.SearchAsync("TEST-E1", TestContext.Current.CancellationToken));
        var own = await reader.PageAsync(null, TestContext.Current.CancellationToken);
        Assert.Equal(fixture.Sql.EmployeeId, own.Person?.Id); Assert.False(own.CanSearchPeople);
    }
    [Fact]
    public async Task Staff_SearchesFullNameOrControlAndIncludesInactiveHistoricalPerson()
    {
        await using var db = fixture.Sql.Medical(); var reader = Reader(db, "doctor", fixture.Sql.DoctorPersonId);
        var name = Assert.Single(await reader.SearchAsync("Laura  Histórica", TestContext.Current.CancellationToken));
        var control = Assert.Single(await reader.SearchAsync("ARCH-999", TestContext.Current.CancellationToken));
        Assert.Equal(fixture.HistoricalPerson, name.Id); Assert.Equal(name.Id, control.Id);
        Assert.Empty(await reader.SearchAsync("L", TestContext.Current.CancellationToken));
        Assert.Empty(await reader.SearchAsync(new string('x', 81), TestContext.Current.CancellationToken));
        Assert.Single(await reader.EventsAsync(name.Id, new DateOnly(2025, 1, 1), new DateOnly(2025, 2, 1), TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData(2027, 2, 27, 2027, 2, 27)]
    [InlineData(2027, 2, 20, 2027, 2, 25)]
    [InlineData(2027, 4, 1, 2027, 3, 8)]
    public async Task Page_FocusesCurrentUpcomingOrLatestApprovedPeriod(int y, int m, int d, int fy, int fm, int fd)
    {
        await using var db = fixture.Sql.Medical();
        var page = await Reader(db, localNow: new DateTime(y, m, d, 12, 0, 0)).PageAsync(null, TestContext.Current.CancellationToken);
        Assert.Equal(new DateOnly(fy, fm, fd), page.FocusDate);
    }
    [Fact]
    public async Task Staff_RequiresPersonSelectionBeforeShowingCalendar()
    {
        await using var db = fixture.Sql.Medical();
        var page = await Reader(db, "doctor", fixture.Sql.DoctorPersonId).PageAsync(null, TestContext.Current.CancellationToken);
        Assert.True(page.CanSearchPeople); Assert.Null(page.Person);
    }
    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Workforce"] = fixture.Sql.Connection,
            ["Medical:DataDirectory"] = Path.Combine(AppContext.BaseDirectory, "App_Data", Guid.NewGuid().ToString("N")),
            ["AllowedHosts"] = "localhost"
        }));
    });
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private async Task LoginAsync(HttpClient client, string user)
    {
        var html = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = user, ["Password"] = fixture.Sql.Password, ["__RequestVerificationToken"] = Token().Match(html).Groups[1].Value }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    [Fact]
    public async Task HttpEmployee_SeesOwnCalendarAndCannotForgeAnotherPerson()
    {
        await using var factory = Factory(); using var client = Client(factory);
        await LoginAsync(client, "employee");
        var html = await client.GetStringAsync("/Leaves/Calendar");
        Assert.Contains("leave-calendar-page", html); Assert.Contains("TEST-E1", html); Assert.DoesNotContain("leave-person-form", html);
        using var denied = await client.GetAsync($"/Leaves/CalendarEvents?personId={fixture.Sql.OtherId}&from=2027-03-01&to=2027-04-01");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var selected = await client.GetAsync($"/Leaves/Calendar?personId={fixture.Sql.OtherId}");
        Assert.Equal(HttpStatusCode.Forbidden, selected.StatusCode);
        using var search = JsonDocument.Parse(await client.GetStringAsync("/Leaves/CalendarPeople?term=TEST-E2"));
        Assert.Equal(0, search.RootElement.GetProperty("results").GetArrayLength());
    }
    [Fact]
    public async Task HttpStaff_SearchesPersonAndLoadsPeriodFeedWithLocalAssets()
    {
        await using var factory = Factory(); using var client = Client(factory);
        using var anonymous = await client.GetAsync("/Leaves/CalendarPeople?term=TEST"); Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        await LoginAsync(client, "doctor");
        var initial = await client.GetStringAsync("/Leaves/Calendar"); Assert.Contains("leave-person-form", initial); Assert.DoesNotContain("id=\"leave-calendar-page\"", initial);
        using var people = JsonDocument.Parse(await client.GetStringAsync("/Leaves/CalendarPeople?term=TEST-E1"));
        var id = people.RootElement.GetProperty("results")[0].GetProperty("id").GetInt32(); Assert.Equal(fixture.Sql.EmployeeId, id);
        var page = await client.GetStringAsync($"/Leaves/Calendar?personId={id}"); Assert.Contains("leave-calendar.js", page); Assert.Contains("fullcalendar/index.global.min.js", page);
        using var response = await client.GetAsync($"/Leaves/CalendarEvents?personId={id}&from=2027-03-01&to=2027-04-01");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        using var periods = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); Assert.Equal(2, periods.RootElement.GetArrayLength());
        using var invalid = await client.GetAsync($"/Leaves/CalendarEvents?personId={id}&from=2027-01-01&to=2028-01-01"); Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }
    [GeneratedRegex(@"name=\x22__RequestVerificationToken\x22[^>]*value=\x22([^\x22]+)\x22")]
    private static partial Regex Token();
}
