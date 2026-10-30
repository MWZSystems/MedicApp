using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
namespace MedicApp.Tests;

[Collection("Sql")]
public partial class HttpTests(SqlFixture fixture)
{
    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Workforce"] = fixture.Connection, ["Medical:DataDirectory"] = Path.Combine(AppContext.BaseDirectory, "App_Data", "TestHost", Guid.NewGuid().ToString("N")), ["AllowedHosts"] = "localhost" }));
    });
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private async Task LoginAsync(HttpClient client, string name)
    {
        var html = await client.GetStringAsync("/Account/Login");
        var token = TokenRegex().Match(html).Groups[1].Value;
        Assert.NotEmpty(token);
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = name, ["Password"] = fixture.Password, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    [Fact]
    public async Task Login_AnonymousAndForgery_ProtectsMutations()
    {
        await using var factory = Factory(); using var client = Client(factory);
        using var anonymous = await client.GetAsync("/Leaves");
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = "employee", ["Password"] = fixture.Password }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task Employee_ClinicalAndAdminRoutes_Forbidden()
    {
        await using var factory = Factory(); using var client = Client(factory);
        await LoginAsync(client, "employee");
        using var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("Tu salud", await home.Content.ReadAsStringAsync());
        using var agenda = await client.GetAsync("/Agenda");
        using var doctors = await client.GetAsync("/Admin/Doctors");
        using var events = await client.GetAsync("/Labor/Events");
        Assert.Equal(HttpStatusCode.Redirect, agenda.StatusCode);
        Assert.Contains("Denied", agenda.Headers.Location?.ToString());
        Assert.Contains("Denied", doctors.Headers.Location?.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, events.StatusCode);
    }
    [Fact]
    public async Task Doctor_PagesRenderAndResourcesAreLocal()
    {
        await using var factory = Factory(); using var client = Client(factory);
        await LoginAsync(client, "doctor");
        foreach (var path in new[] { "/", "/Appointments", "/Appointments/Book", "/Leaves", "/Leaves/Create", "/Agenda", "/Admin/Doctors", "/Admin/Access", "/Admin/Audit" })
        {
            using var response = await client.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            var html = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("cdn.jsdelivr", html);
            Assert.DoesNotContain("fonts.googleapis", html);
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
            Assert.True(response.Headers.Contains("Content-Security-Policy"));
        }
    }

    [Fact]
    public async Task Labor_AuthorizedReader_ReceivesOnlyLaborContract()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        await LoginAsync(client, "doctor");
        using var events = await client.GetAsync("/Labor/Events?after=0&limit=100");
        Assert.Equal(HttpStatusCode.OK, events.StatusCode);
        var json = await events.Content.ReadAsStringAsync();
        Assert.Contains("events", json);
        Assert.DoesNotContain("cancellationReason", json);
        Assert.DoesNotContain("professionalLicense", json);
        var from = fixture.Tomorrow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "T00:00:00Z";
        var to = fixture.Tomorrow.AddDays(30).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "T00:00:00Z";
        using var restrictions = await client.GetAsync($"/Labor/Restrictions?personId={fixture.EmployeeId}&from={from}&to={to}");
        Assert.Equal(HttpStatusCode.OK, restrictions.StatusCode);
    }

    [Fact]
    public async Task Calendar_AuthenticatedPage_HasOnlyDateAndTimeForBooking()
    {
        await using var factory = Factory(); using var client = Client(factory);
        await LoginAsync(client, "employee");
        var html = await client.GetStringAsync("/Appointments");
        Assert.Contains("appointments-calendar", html);
        Assert.Contains("name=\"Date\"", html);
        Assert.Contains("name=\"Time\"", html);
        Assert.DoesNotContain("name=\"PersonId\"", html);
        Assert.DoesNotContain("name=\"WindowId\"", html);
        Assert.Contains("fullcalendar/index.global.min.js", html);
        Assert.Contains("icons.svg#calendar", html);
        using var denied = await client.PostAsync("/Appointments/Reserve", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
    }
    [Fact]
    public async Task Calendar_ReserveEndpoint_IgnoresForgedEmployeeAndDoctor()
    {
        await using var factory = Factory(); using var client = Client(factory);
        await LoginAsync(client, "employee");
        var html = await client.GetStringAsync("/Appointments");
        var token = TokenRegex().Match(html).Groups[1].Value;
        var at = SqlFixture.Clock().Local(fixture.Tomorrow.AddHours(6));
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        using var response = await client.PostAsync("/Appointments/Reserve", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Date"] = at.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["Time"] = at.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            ["RequestKey"] = Guid.NewGuid().ToString(),
            ["PersonId"] = fixture.OtherId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["DoctorId"] = "999999",
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = document.RootElement.GetProperty("id").GetInt32();
        await using var db = fixture.Medical();
        var saved = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Appointments, x => x.Id == id);
        Assert.Equal(fixture.EmployeeId, saved.PersonId);
        Assert.Equal(fixture.DoctorId, saved.DoctorId);
    }
    [Fact]
    public async Task Calendar_InvalidRange_IsRejected()
    {
        await using var factory = Factory(); using var client = Client(factory);
        await LoginAsync(client, "employee");
        using var response = await client.GetAsync("/Appointments/Events?from=2026-01-01&to=2026-12-31");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
    [Fact]
    public async Task Configuration_AdminCanSaveAndNextRequestUsesPersistentRules()
    {
        await using var factory = Factory(); using var client = Client(factory);
        await LoginAsync(client, "doctor");
        var html = await client.GetStringAsync("/Configuration");
        Assert.Contains("name=\"Open24Hours\"", html);
        var token = TokenRegex().Match(html).Groups[1].Value;
        var revision = Regex.Match(html, "name=\"Revision\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(revision);
        using var saved = await client.PostAsync("/Configuration", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["BookingHorizonDays"] = "21",
            ["Open24Hours"] = "true",
            ["OpeningTime"] = "08:00",
            ["ClosingTime"] = "18:00",
            ["DefaultSlotMinutes"] = "20",
            ["Revision"] = revision,
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        html = await client.GetStringAsync("/Configuration");
        Assert.Matches("name=\"BookingHorizonDays\"[^>]*value=\"21\"", html);
        using var csrf = await client.PostAsync("/Configuration", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        using var demo = await client.GetAsync("/Configuration/Demo");
        Assert.Equal(HttpStatusCode.OK, demo.StatusCode);
    }
    [Fact]
    public async Task Configuration_EmployeeCannotReadSettingsOrDemo()
    {
        await using var factory = Factory(); using var client = Client(factory);
        await LoginAsync(client, "employee");
        foreach (var path in new[] { "/Configuration", "/Configuration/Demo", "/Configuration/DemoEvents?from=2026-10-01&to=2026-11-01" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("Denied", response.Headers.Location?.ToString());
        }
    }
    [GeneratedRegex(@"name=\x22__RequestVerificationToken\x22[^>]*value=\x22([^\x22]+)\x22")]
    private static partial Regex TokenRegex();
}

