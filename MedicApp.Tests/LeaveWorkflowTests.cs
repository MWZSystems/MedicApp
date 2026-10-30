using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MedicApp.Models.Entities;
using MedicApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace MedicApp.Tests;

[CollectionDefinition("LeaveWorkflowSql", DisableParallelization = true)]
public class LeaveWorkflowGroup : ICollectionFixture<SqlFixture>;
[Collection("LeaveWorkflowSql")]
public partial class LeaveWorkflowTests(SqlFixture fixture)
{
    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Workforce"] = fixture.Connection,
            ["Medical:DataDirectory"] = Path.Combine(AppContext.BaseDirectory, "App_Data", Guid.NewGuid().ToString("N")),
            ["AllowedHosts"] = "localhost"
        }));
    });
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private async Task LoginAsync(HttpClient client, string name)
    {
        var html = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = name, ["Password"] = fixture.Password, ["__RequestVerificationToken"] = Token().Match(html).Groups[1].Value }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    [Fact]
    public async Task Doctor_RegistersDraftThenAuthorizesToCalendarAndCancellationRemovesPeriod()
    {
        await using var factory = Factory(); using var client = Client(factory);
        await LoginAsync(client, "doctor");
        var capture = await client.GetStringAsync("/Leaves/Create");
        Assert.Contains("Registro de incapacidades", capture); Assert.Contains("Calendario de incapacidades", capture);
        var token = Token().Match(capture).Groups[1].Value;
        var start = SqlFixture.Clock().Local(fixture.Tomorrow).Date.AddHours(8);
        var end = start.Date.AddDays(3);
        var reference = "WORKFLOW-" + Guid.NewGuid().ToString("N");
        using var created = await client.PostAsync("/Leaves/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["PersonId"] = fixture.EmployeeId.ToString(CultureInfo.InvariantCulture),
            ["Reference"] = reference,
            ["StartsAt"] = start.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            ["EndsAt"] = end.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            ["__RequestVerificationToken"] = token
        }));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var registry = await client.GetStringAsync(created.Headers.Location);
        Assert.Contains(reference, registry); Assert.Contains("Incapacidad registrada en borrador", registry);
        await using var db = fixture.Medical();
        var row = await db.Leaves.SingleAsync(x => x.Reference == reference, TestContext.Current.CancellationToken);
        Assert.Equal(LeaveStatus.Draft, row.Status);
        var feed = $"/Leaves/CalendarEvents?personId={fixture.EmployeeId}&from={start:yyyy-MM-dd}&to={end.AddDays(1):yyyy-MM-dd}";
        using (var before = JsonDocument.Parse(await client.GetStringAsync(feed))) { Assert.Equal(0, before.RootElement.GetArrayLength()); }
        async Task ChangeAsync(string status)
        {
            using var response = await client.PostAsync("/Leaves/Change", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = row.Id.ToString(CultureInfo.InvariantCulture),
                ["Status"] = status,
                ["Reason"] = "Prueba sintética del flujo",
                ["Version"] = Convert.ToBase64String(row.RowVersion),
                ["__RequestVerificationToken"] = Token().Match(registry).Groups[1].Value
            }));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            await db.Entry(row).ReloadAsync(TestContext.Current.CancellationToken);
        }
        await ChangeAsync("2");
        Assert.Equal(LeaveStatus.Approved, row.Status);
        using (var visible = JsonDocument.Parse(await client.GetStringAsync(feed)))
        {
            var period = Assert.Single(visible.RootElement.EnumerateArray()); Assert.Equal(row.Id.ToString(CultureInfo.InvariantCulture), period.GetProperty("id").GetString());
        }
        var approvedRegistry = await client.GetStringAsync("/Leaves");
        Assert.Contains($"/Leaves/Calendar?personId={fixture.EmployeeId}", approvedRegistry);
        await ChangeAsync("3"); Assert.Equal(LeaveStatus.Cancelled, row.Status);
        using var removed = JsonDocument.Parse(await client.GetStringAsync(feed)); Assert.Equal(0, removed.RootElement.GetArrayLength());
    }
    [Fact]
    public async Task Employee_HasTwoSectionsButCannotCaptureOrChangeIncapacities()
    {
        await using var factory = Factory(); using var client = Client(factory);
        await LoginAsync(client, "employee");
        var registry = await client.GetStringAsync("/Leaves");
        Assert.Contains("Registro de incapacidades", registry); Assert.Contains("Calendario de incapacidades", registry);
        Assert.DoesNotContain("href=\"/Leaves/Create\"", registry);
        var calendar = await client.GetStringAsync("/Leaves/Calendar");
        Assert.Contains("Registro de incapacidades", calendar); Assert.Contains("leave-calendar-page", calendar);
        using var capture = await client.GetAsync("/Leaves/Create"); Assert.Equal(HttpStatusCode.Redirect, capture.StatusCode); Assert.Contains("Denied", capture.Headers.Location?.ToString());
        using var change = await client.PostAsync("/Leaves/Change", new FormUrlEncodedContent(new Dictionary<string, string> { ["Id"] = "1", ["Status"] = "2", ["Reason"] = "Sin permiso", ["Version"] = Convert.ToBase64String(new byte[8]), ["__RequestVerificationToken"] = Token().Match(registry).Groups[1].Value }));
        Assert.Equal(HttpStatusCode.Redirect, change.StatusCode); Assert.Contains("Denied", change.Headers.Location?.ToString());
    }
    [GeneratedRegex(@"name=\x22__RequestVerificationToken\x22[^>]*value=\x22([^\x22]+)\x22")]
    private static partial Regex Token();
}
