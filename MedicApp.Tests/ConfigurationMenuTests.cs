using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MedicApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace MedicApp.Tests;

[Collection("Sql")]
public partial class ConfigurationMenuTests(SqlFixture fixture) : IAsyncDisposable
{
    private readonly string data = Path.Combine(Path.GetTempPath(), "MedicAppConfigurationTests", Guid.NewGuid().ToString("N"));
    private string FileName => Path.Combine(data, "Configuration", "medical.json");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    public ValueTask DisposeAsync()
    {
        var actual = Path.GetFullPath(data); var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MedicAppConfigurationTests"));
        if (!string.Equals(Path.GetDirectoryName(actual), expected, StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException("Unsafe test cleanup"); }
        if (Directory.Exists(actual)) { Directory.Delete(actual, true); }
        GC.SuppressFinalize(this); return ValueTask.CompletedTask;
    }
    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Workforce"] = fixture.Connection, ["Medical:DataDirectory"] = data, ["Medical:DisplayName"] = "MedicApp", ["AllowedHosts"] = "localhost" }));
    });
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private async Task LoginAsync(HttpClient client, string user = "doctor")
    {
        var html = await client.GetStringAsync("/Account/Login"); using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = user, ["Password"] = fixture.Password, ["__RequestVerificationToken"] = Token().Match(html).Groups[1].Value })); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private static Dictionary<string, string> Form(string html) => new()
    {
        ["BookingHorizonDays"] = "9",
        ["DefaultSlotMinutes"] = "20",
        ["Open24Hours"] = "true",
        ["OpeningTime"] = "08:00",
        ["ClosingTime"] = "18:00",
        ["DemoBookingEnabled"] = "false",
        ["General.DisplayName"] = "Clínica <ejemplo>",
        ["General.TimeZoneId"] = "UTC",
        ["Documents.MaxFileSizeMb"] = "2",
        ["Documents.MaxBatchSizeMb"] = "4",
        ["Documents.MaxFilesPerUpload"] = "2",
        ["Documents.MaxDocumentsPerLeave"] = "6",
        ["Documents.MaxDocumentsPerPatient"] = "8",
        ["Inventory.ExpiryWarningDays"] = "9",
        ["Revision"] = Revision().Match(html).Groups[1].Value,
        ["__RequestVerificationToken"] = Token().Match(html).Groups[1].Value
    };
    [Fact]
    public async Task AllSections_SavePersistAcrossRequestsEnforceDocumentLimitsAndAuditChanges()
    {
        await using var factory = Factory(); using var client = Client(factory); await LoginAsync(client);
        var page = await client.GetStringAsync("/Configuration");
        foreach (var id in new[] { "general", "appointments", "documents", "clinical", "inventory", "administration", "demo" }) { Assert.Contains("id=\"" + id + "\"", page); }
        Assert.Contains("id=\"configuration-menu\"", page);
        using var response = await client.PostAsync("/Configuration", new FormUrlEncodedContent(Form(page))); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = await client.GetStringAsync("/Configuration"); Assert.Contains("Cl", saved); Assert.Contains("&lt;ejemplo&gt;", saved); Assert.DoesNotContain("<ejemplo>", saved);
        using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(FileName, Ct)); Assert.Equal("UTC", json.RootElement.GetProperty("Medical").GetProperty("TimeZoneId").GetString()); Assert.Equal(8, json.RootElement.GetProperty("Documents").GetProperty("MaxDocumentsPerPatient").GetInt32());
        Assert.DoesNotContain("ConnectionStrings", json.RootElement.ToString()); Assert.DoesNotContain("DataDirectory", json.RootElement.ToString());
        using var scope = factory.Services.CreateScope(); var docs = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<DocumentOptions>>().Value;
        Assert.Equal(2, docs.MaxFileSizeMb); Assert.Equal(4, docs.MaxBatchSizeMb); Assert.Equal(6, docs.MaxDocumentsPerLeave);
        Assert.Equal(9, scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<InventoryOptions>>().Value.ExpiryWarningDays);
        Assert.Equal("UTC", scope.ServiceProvider.GetRequiredService<MedicalClock>().Zone.Id);
        var bytes = new byte[3 * 1048576]; using var stream = new MemoryStream(bytes); var file = new Microsoft.AspNetCore.Http.FormFile(stream, 0, bytes.Length, "Files", "too-large.pdf");
        var rejected = await Assert.ThrowsAsync<MedicalRuleException>(() => scope.ServiceProvider.GetRequiredService<EvidenceStorage>().PrepareAsync([file], Ct)); Assert.Equal("EvidenceLimits", rejected.Message);
        await using var db = fixture.Medical(); Assert.True(await db.Audit.AnyAsync(x => x.EntityType == "ApplicationSetting" && x.Reason.StartsWith("Documents.MaxFileSizeMb: 10 -> 2"), Ct));
        Assert.True(await db.Audit.AnyAsync(x => x.EntityType == "ApplicationSetting" && x.Reason.StartsWith("Medical.DisplayName: MedicApp -> Clínica <ejemplo>"), Ct));
        Assert.All(await db.Audit.Where(x => x.EntityType == "ApplicationSetting" || x.EntityType == "ApplicationConfiguration").Select(x => x.Reason).ToListAsync(Ct), x => Assert.True(x.Length <= 500));
    }
    [Theory]
    [InlineData("General.DisplayName", "   ")]
    [InlineData("General.TimeZoneId", "Mars/Fake")]
    [InlineData("Documents.MaxFileSizeMb", "11")]
    [InlineData("Documents.MaxBatchSizeMb", "1")]
    [InlineData("Documents.MaxFilesPerUpload", "6")]
    [InlineData("Documents.MaxDocumentsPerLeave", "1")]
    [InlineData("Documents.MaxDocumentsPerPatient", "1001")]
    [InlineData("Inventory.ExpiryWarningDays", "0")]
    [InlineData("Inventory.ExpiryWarningDays", "366")]
    [InlineData("BookingHorizonDays", "0")]
    public async Task InvalidSettings_DoNotWriteConfigurationOrAudit(string key, string value)
    {
        await using var factory = Factory(); using var client = Client(factory); await LoginAsync(client); await using var db = fixture.Medical(); var before = await db.Audit.CountAsync(Ct);
        var form = Form(await client.GetStringAsync("/Configuration")); form[key] = value;
        using var response = await client.PostAsync("/Configuration", new FormUrlEncodedContent(form)); Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.False(File.Exists(FileName)); Assert.Equal(before, await db.Audit.CountAsync(Ct));
    }
    [Fact]
    public async Task StaleRevision_RejectsOverwriteAndLegacySchedulingPostPreservesNewSections()
    {
        await using var factory = Factory(); using var client = Client(factory); await LoginAsync(client);
        var stale = Form(await client.GetStringAsync("/Configuration")); using var first = await client.PostAsync("/Configuration", new FormUrlEncodedContent(stale)); Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        stale["General.DisplayName"] = "Lost update"; using var conflict = await client.PostAsync("/Configuration", new FormUrlEncodedContent(stale)); Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var form = Form(await client.GetStringAsync("/Configuration")); foreach (var key in form.Keys.Where(x => x.StartsWith("General.", StringComparison.Ordinal) || x.StartsWith("Documents.", StringComparison.Ordinal) || x.StartsWith("Inventory.", StringComparison.Ordinal)).ToArray()) { form.Remove(key); }
        form["BookingHorizonDays"] = "15";
        using var second = await client.PostAsync("/Configuration", new FormUrlEncodedContent(form)); Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(FileName, Ct)); Assert.Equal("Clínica <ejemplo>", json.RootElement.GetProperty("Medical").GetProperty("DisplayName").GetString()); Assert.Equal(15, json.RootElement.GetProperty("Medical").GetProperty("BookingHorizonDays").GetInt32()); Assert.Equal(2, json.RootElement.GetProperty("Documents").GetProperty("MaxFileSizeMb").GetInt32()); Assert.Equal(9, json.RootElement.GetProperty("Inventory").GetProperty("ExpiryWarningDays").GetInt32());
    }
    [Fact]
    public async Task LegacySchedulingFileWithUtf8Bom_PreservesDefaultsAndCanBeUpgraded()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
        await File.WriteAllTextAsync(FileName, "{\"Medical\":{\"BookingHorizonDays\":11,\"Open24Hours\":true,\"OpeningTime\":\"08:00\",\"ClosingTime\":\"18:00\",\"DefaultSlotMinutes\":30,\"DemoBookingEnabled\":true}}", new UTF8Encoding(true), Ct);
        await using var factory = Factory(); using var client = Client(factory); await LoginAsync(client); var page = await client.GetStringAsync("/Configuration");
        Assert.Matches("name=\"BookingHorizonDays\"[^>]*value=\"11\"", page); var form = Form(page);
        using var response = await client.PostAsync("/Configuration", new FormUrlEncodedContent(form)); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(FileName, Ct)); Assert.True(json.RootElement.TryGetProperty("Documents", out _)); Assert.True(json.RootElement.TryGetProperty("Inventory", out _));
    }
    [Fact]
    public async Task EmployeeCannotSeeMenuOrSaveAndAdminRequiresAntiforgery()
    {
        await using var factory = Factory(); using var employee = Client(factory); await LoginAsync(employee, "employee"); var home = await employee.GetStringAsync("/"); Assert.DoesNotContain("id=\"configuration-menu\"", home);
        using var denied = await employee.GetAsync("/Configuration"); Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode); Assert.Contains("Denied", denied.Headers.Location?.ToString());
        using var admin = Client(factory); await LoginAsync(admin); var form = Form(await admin.GetStringAsync("/Configuration"));
        using var forbidden = await employee.PostAsync("/Configuration", new FormUrlEncodedContent(form)); Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode); Assert.Contains("Denied", forbidden.Headers.Location?.ToString());
        form.Remove("__RequestVerificationToken"); using var forged = await admin.PostAsync("/Configuration", new FormUrlEncodedContent(form)); Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode); Assert.False(File.Exists(FileName));
    }
    [Fact]
    public async Task InventoryAlert_UsesConfiguredDaysOnNextRequest()
    {
        await using var db = fixture.Medical(); var name = "CONFIG-" + Guid.NewGuid().ToString("N");
        db.StockItems.Add(new() { Code = name, Name = name, Unit = "pieza", ExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(12), UpdatedById = "doctor", CurrentStock = 5 }); await db.SaveChangesAsync(Ct);
        await using var factory = Factory(); using var client = Client(factory); await LoginAsync(client);
        var form = Form(await client.GetStringAsync("/Configuration")); using var first = await client.PostAsync("/Configuration", new FormUrlEncodedContent(form)); Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        var inventory = await client.GetStringAsync("/Inventory?query=" + name); Assert.DoesNotContain("Caduca en 9", inventory);
        form = Form(await client.GetStringAsync("/Configuration")); form["Inventory.ExpiryWarningDays"] = "14";
        using var second = await client.PostAsync("/Configuration", new FormUrlEncodedContent(form)); Assert.Equal(HttpStatusCode.Redirect, second.StatusCode); inventory = await client.GetStringAsync("/Inventory?query=" + name); Assert.Contains("Caduca en 14", inventory);
    }
    [Fact]
    public async Task FileWriteFailure_RollsBackAuditAndLeavesNoPartialConfiguration()
    {
        await using var factory = Factory(); using var client = Client(factory); await LoginAsync(client); var form = Form(await client.GetStringAsync("/Configuration"));
        Directory.CreateDirectory(FileName); await using var db = fixture.Medical(); var before = await db.Audit.CountAsync(Ct);
        using var response = await client.PostAsync("/Configuration", new FormUrlEncodedContent(form)); Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode); Assert.Equal(before, await db.Audit.CountAsync(Ct)); Assert.False(File.Exists(FileName)); Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(FileName)!, "*.tmp"));
    }
    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex Token();
    [GeneratedRegex("name=\"Revision\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex Revision();
}
