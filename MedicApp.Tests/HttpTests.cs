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
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Workforce"] = fixture.Connection, ["Medical:DataDirectory"] = Path.Combine(AppContext.BaseDirectory, "App_Data", "TestHost"), ["AllowedHosts"] = "localhost" }));
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

    [GeneratedRegex(@"name=\x22__RequestVerificationToken\x22[^>]*value=\x22([^\x22]+)\x22")]
    private static partial Regex TokenRegex();
}

