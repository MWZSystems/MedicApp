using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Models.ViewModels;
using MedicApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
namespace MedicApp.Tests;

public sealed class ClinicalFixture : IAsyncLifetime
{
    public SqlFixture Sql { get; } = new();
    public async ValueTask InitializeAsync()
    {
        await Sql.InitializeAsync(); await using var db = Sql.Medical();
        db.AccessProfiles.Add(new() { UserId = "other", ManageClinicalRecords = true, ManageInventory = true, UpdatedById = "doctor" });
        await db.SaveChangesAsync();
    }
    public ValueTask DisposeAsync() => Sql.DisposeAsync();
}
[CollectionDefinition("ClinicalSql", DisableParallelization = true)]
public class ClinicalGroup : ICollectionFixture<ClinicalFixture>;
[Collection("ClinicalSql")]
public partial class ClinicalTests(ClinicalFixture fixture) : IAsyncDisposable
{
    private readonly Trainify.Data.TrainifyDbContext shared = fixture.Sql.Shared();
    private readonly string data = Path.Combine(Path.GetTempPath(), "MedicAppClinicalTests", Guid.NewGuid().ToString("N"));
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    public async ValueTask DisposeAsync()
    {
        var actual = Path.GetFullPath(data); var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MedicAppClinicalTests"));
        if (!string.Equals(Path.GetDirectoryName(actual), parent, StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException("Unsafe test cleanup"); }
        if (Directory.Exists(actual)) { Directory.Delete(actual, true); }
        await shared.DisposeAsync(); GC.SuppressFinalize(this);
    }
    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Workforce"] = fixture.Sql.Connection, ["Medical:DataDirectory"] = data, ["AllowedHosts"] = "localhost" }));
    });
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private static string Number(int id) => id.ToString(CultureInfo.InvariantCulture);
    private async Task LoginAsync(HttpClient client, string user)
    {
        var html = await client.GetStringAsync("/Account/Login"); using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = user, ["Password"] = fixture.Sql.Password, ["__RequestVerificationToken"] = Token().Match(html).Groups[1].Value })); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private InventoryService Inventory(MedicDbContext db, string user, int person) => new(db, new MedicalAccess(db, SqlFixture.Context(user, person)), SqlFixture.Service(db, shared, user, person), SqlFixture.Clock());
    private async Task<int> ItemAsync(decimal stock = 0, DateOnly? expiry = null)
    {
        await using var db = fixture.Sql.Medical(); var service = Inventory(db, "doctor", fixture.Sql.DoctorPersonId);
        var id = await service.CreateAsync(new() { Code = Guid.NewGuid().ToString("N"), Name = "Synthetic test stock", Unit = "pieza", ExpiresOn = expiry }, Ct);
        if (stock > 0) { await service.MoveAsync(new() { StockItemId = id, Quantity = stock, Reason = "Initial test stock" }, Ct); }
        return id;
    }
    [Fact]
    public async Task ClinicalVisit_OptionalFieldsAndNurseAccessPersistOnlyOneVisitOnRetry()
    {
        await using var factory = Factory(); using var nurse = Client(factory); await LoginAsync(nurse, "other");
        var html = await nurse.GetStringAsync("/ClinicalRecords/Create?personId=" + Number(fixture.Sql.EmployeeId)); var key = Guid.NewGuid();
        var values = new Dictionary<string, string> { ["PersonId"] = Number(fixture.Sql.EmployeeId), ["VisitedAt"] = SqlFixture.Clock().Local(DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture), ["RequestKey"] = key.ToString(), ["__RequestVerificationToken"] = Token().Match(html).Groups[1].Value };
        for (var i = 0; i < 2; i++) { using var response = await nurse.PostAsync("/ClinicalRecords/Create", new FormUrlEncodedContent(values)); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); }
        await using var db = fixture.Sql.Medical(); var visits = await db.ClinicalVisits.Where(x => x.RequestKey == key).ToListAsync(Ct); var visit = Assert.Single(visits); Assert.Equal("other", visit.UpdatedById); Assert.Null(visit.Weight); Assert.Empty(visit.Notes);
        Assert.True(await db.Audit.AnyAsync(x => x.EntityType == "ClinicalVisit" && x.EntityId == visit.Id && x.Operation == "Create", Ct));
        var record = WebUtility.HtmlDecode(await nurse.GetStringAsync("/ClinicalRecords?personId=" + Number(fixture.Sql.EmployeeId))); Assert.Contains("Expediente clínico", record); Assert.Contains("Consulta #" + Number(visit.Id), record); Assert.Contains("other", record);
    }
    [Theory]
    [InlineData("Temperature", "70")]
    [InlineData("OxygenSaturation", "150")]
    [InlineData("Systolic", "19")]
    public async Task ClinicalVisit_RejectsInvalidVitals(string field, string value)
    {
        await using var factory = Factory(); using var doctor = Client(factory); await LoginAsync(doctor, "doctor"); var key = Guid.NewGuid();
        var html = await doctor.GetStringAsync("/ClinicalRecords/Create?personId=" + Number(fixture.Sql.EmployeeId));
        using var response = await doctor.PostAsync("/ClinicalRecords/Create", new FormUrlEncodedContent(new Dictionary<string, string> { ["PersonId"] = Number(fixture.Sql.EmployeeId), ["VisitedAt"] = "2026-10-01T10:00", ["RequestKey"] = key.ToString(), [field] = value, ["__RequestVerificationToken"] = Token().Match(html).Groups[1].Value })); Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await using var db = fixture.Sql.Medical(); Assert.False(await db.ClinicalVisits.AnyAsync(x => x.RequestKey == key, Ct));
    }
    [Fact]
    public async Task ClinicalRecord_DeniesEmployeeAndInventoryOnlyPermissionDoesNotGrantClinical()
    {
        await using var factory = Factory(); using var employee = Client(factory); await LoginAsync(employee, "employee");
        foreach (var url in new[] { "/ClinicalRecords?personId=" + Number(fixture.Sql.EmployeeId), "/ClinicalRecords/People?term=TEST", "/Inventory", "/ClinicalRecords/Download/1" })
        { using var denied = await employee.GetAsync(url); Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode); Assert.Contains("Denied", denied.Headers.Location?.ToString()); }
        await using var db = fixture.Sql.Medical(); var access = new MedicalAccess(db, SqlFixture.Context("employee", fixture.Sql.EmployeeId));
        db.AccessProfiles.Add(new() { UserId = "employee", ManageInventory = true, UpdatedById = "doctor" }); await db.SaveChangesAsync(Ct);
        try { Assert.True(await access.CanUseInventoryAsync(Ct)); Assert.False(await access.CanUseClinicalRecordsAsync(Ct)); }
        finally { var profile = await db.AccessProfiles.SingleAsync(x => x.UserId == "employee", Ct); db.AccessProfiles.Remove(profile); await db.SaveChangesAsync(Ct); }
    }
    [Fact]
    public async Task PatientFiles_EncryptedRoundTripIdempotencyAndPatientAssociation()
    {
        await using var factory = Factory(); using var doctor = Client(factory); await LoginAsync(doctor, "doctor"); var key = Guid.NewGuid();
        var html = await doctor.GetStringAsync("/ClinicalRecords?personId=" + Number(fixture.Sql.EmployeeId));
        for (var i = 0; i < 2; i++)
        {
            using var form = new MultipartFormDataContent(); form.Add(new StringContent(Number(fixture.Sql.EmployeeId)), "PersonId"); form.Add(new StringContent(key.ToString()), "RequestKey"); form.Add(new StringContent("1"), "Kind"); form.Add(new StringContent(Token().Match(html).Groups[1].Value), "__RequestVerificationToken"); var content = new ByteArrayContent(Pdf); content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf"); form.Add(content, "Files", "Synthetic-prescription.pdf");
            using var uploaded = await doctor.PostAsync("/ClinicalRecords/Upload", form); Assert.Equal(HttpStatusCode.Redirect, uploaded.StatusCode);
        }
        await using var db = fixture.Sql.Medical(); var document = Assert.Single(await db.PatientDocuments.Where(x => x.RequestKey == key).ToListAsync(Ct)); Assert.Equal(PatientDocumentKind.Prescription, document.Kind);
        var encrypted = await File.ReadAllBytesAsync(Path.Combine(data, "Evidence", document.StorageKey[..2], document.StorageKey + ".dat"), Ct); Assert.NotEqual(Pdf, encrypted);
        using var download = await doctor.GetAsync("/ClinicalRecords/Download/" + Number(document.Id)); Assert.Equal(HttpStatusCode.OK, download.StatusCode); Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType); Assert.Equal(Pdf, await download.Content.ReadAsByteArrayAsync(Ct));
        using var employee = Client(factory); await LoginAsync(employee, "employee"); using var denied = await employee.GetAsync("/ClinicalRecords/Download/" + Number(document.Id)); Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode); Assert.Contains("Denied", denied.Headers.Location?.ToString());
        Assert.True(await db.Audit.AnyAsync(x => x.EntityType == "PatientDocument" && x.EntityId == document.Id && x.Operation == "Download", Ct));
    }
    [Fact]
    public async Task ClinicalFiles_RejectCrossPatientVisitInvalidBatchAndMissingAntiforgery()
    {
        await using var factory = Factory(); using var doctor = Client(factory); await LoginAsync(doctor, "doctor");
        await using var db = fixture.Sql.Medical(); var visit = new ClinicalVisit { PersonId = fixture.Sql.OtherId, VisitedAt = DateTime.UtcNow, RequestKey = Guid.NewGuid(), UpdatedById = "doctor" }; db.ClinicalVisits.Add(visit); await db.SaveChangesAsync(Ct);
        var html = await doctor.GetStringAsync("/ClinicalRecords?personId=" + Number(fixture.Sql.EmployeeId));
        foreach (var attempt in new[] { 0, 1, 2 })
        {
            var key = Guid.NewGuid(); using var form = new MultipartFormDataContent(); form.Add(new StringContent(Number(fixture.Sql.EmployeeId)), "PersonId"); form.Add(new StringContent(key.ToString()), "RequestKey"); if (attempt != 2) { form.Add(new StringContent(Token().Match(html).Groups[1].Value), "__RequestVerificationToken"); }
            if (attempt == 0) { form.Add(new StringContent(Number(visit.Id)), "ClinicalVisitId"); }
            form.Add(new ByteArrayContent(attempt == 1 ? Encoding.ASCII.GetBytes("invalid") : Pdf), "Files", "test.pdf"); using var response = await doctor.PostAsync("/ClinicalRecords/Upload", form);
            Assert.Equal(attempt == 0 ? HttpStatusCode.NotFound : attempt == 1 ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(await db.PatientDocuments.AnyAsync(x => x.RequestKey == key, Ct));
        }
        Assert.False(Directory.Exists(Path.Combine(data, "Evidence")));
    }
    [Fact]
    public async Task Inventory_MovementsAreIdempotentAndCannotOverdraw()
    {
        var id = await ItemAsync(10); await using var db = fixture.Sql.Medical(); var service = Inventory(db, "other", fixture.Sql.OtherId);
        var movement = new StockMovementForm { StockItemId = id, Kind = StockMovementKind.Exit, Quantity = 3.5m, Reason = "Test delivery" };
        await service.MoveAsync(movement, Ct); await service.MoveAsync(movement, Ct); Assert.Equal(6.5m, (await db.StockItems.SingleAsync(x => x.Id == id, Ct)).CurrentStock); Assert.Equal(1, await db.StockMovements.CountAsync(x => x.RequestKey == movement.RequestKey, Ct));
        var error = await Assert.ThrowsAsync<MedicalRuleException>(() => service.MoveAsync(new() { StockItemId = id, Kind = StockMovementKind.Exit, Quantity = 7, Reason = "Overdraw" }, Ct)); Assert.Equal("StockInsufficient", error.Message);
        movement.Quantity = 1; var conflict = await Assert.ThrowsAsync<MedicalRuleException>(() => service.MoveAsync(movement, Ct)); Assert.Equal("Concurrency", conflict.Message);
    }
    [Fact]
    public async Task Inventory_ConcurrentExitsPreserveBalanceAndLedger()
    {
        var id = await ItemAsync(10);
        async Task<bool> ExitAsync()
        {
            await using var db = fixture.Sql.Medical(); var service = Inventory(db, "doctor", fixture.Sql.DoctorPersonId);
            try { await service.MoveAsync(new() { StockItemId = id, Kind = StockMovementKind.Exit, Quantity = 7, Reason = "Concurrent test" }, Ct); return true; }
            catch (MedicalRuleException error) when (error.Message == "StockInsufficient") { return false; }
        }
        var results = await Task.WhenAll(ExitAsync(), ExitAsync()); Assert.Single(results, x => x); await using var verify = fixture.Sql.Medical(); Assert.Equal(3, (await verify.StockItems.SingleAsync(x => x.Id == id, Ct)).CurrentStock); Assert.Equal(2, await verify.StockMovements.CountAsync(x => x.StockItemId == id, Ct));
    }
    [Fact]
    public async Task Inventory_ExpiredStockCanBeWrittenOffButNotDelivered()
    {
        var id = await ItemAsync(10, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2)); await using var db = fixture.Sql.Medical(); var service = Inventory(db, "doctor", fixture.Sql.DoctorPersonId);
        var error = await Assert.ThrowsAsync<MedicalRuleException>(() => service.MoveAsync(new() { StockItemId = id, Kind = StockMovementKind.Exit, Quantity = 1, Reason = "Expired test" }, Ct)); Assert.Equal("StockExpired", error.Message);
        await service.MoveAsync(new() { StockItemId = id, Kind = StockMovementKind.Loss, Quantity = 10, Reason = "Expired test write-off" }, Ct); Assert.Equal(0, (await db.StockItems.SingleAsync(x => x.Id == id, Ct)).CurrentStock);
    }
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.0001")]
    public async Task Inventory_RejectsInvalidQuantities(string quantity)
    {
        var id = await ItemAsync(); await using var db = fixture.Sql.Medical(); var service = Inventory(db, "doctor", fixture.Sql.DoctorPersonId);
        await Assert.ThrowsAsync<MedicalRuleException>(() => service.MoveAsync(new() { StockItemId = id, Quantity = decimal.Parse(quantity, CultureInfo.InvariantCulture), Reason = "Bad quantity" }, Ct)); Assert.False(await db.StockMovements.AnyAsync(x => x.StockItemId == id, Ct));
    }
    [Fact]
    public async Task Inventory_WebFormsAndClinicalNotesEscapeHtmlAndRequireAntiforgery()
    {
        await using var factory = Factory(); using var doctor = Client(factory); await LoginAsync(doctor, "doctor");
        var html = await doctor.GetStringAsync("/Inventory/Create"); var code = Guid.NewGuid().ToString("N");
        using var created = await doctor.PostAsync("/Inventory/Create", new FormUrlEncodedContent(new Dictionary<string, string> { ["Code"] = code, ["Name"] = "<script>test</script>", ["Unit"] = "pieza", ["MinimumStock"] = "2", ["__RequestVerificationToken"] = Token().Match(html).Groups[1].Value })); Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var itemHtml = await doctor.GetStringAsync(created.Headers.Location); Assert.Contains("&lt;script&gt;test&lt;/script&gt;", itemHtml); Assert.DoesNotContain("<script>test</script>", itemHtml);
        await using var db = fixture.Sql.Medical(); var normalizedCode = code.ToUpperInvariant(); var item = await db.StockItems.SingleAsync(x => x.Code == normalizedCode, Ct);
        using var forged = await doctor.PostAsync("/Inventory/Move", new FormUrlEncodedContent(new Dictionary<string, string> { ["StockItemId"] = Number(item.Id), ["Quantity"] = "5", ["Reason"] = "Forgery", ["RequestKey"] = Guid.NewGuid().ToString() })); Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.False(await db.StockMovements.AnyAsync(x => x.StockItemId == item.Id, Ct));
        using var valid = await doctor.PostAsync("/Inventory/Move", new FormUrlEncodedContent(new Dictionary<string, string> { ["StockItemId"] = Number(item.Id), ["Quantity"] = "5.5", ["Kind"] = "0", ["Reason"] = "Web test entry", ["RequestKey"] = Guid.NewGuid().ToString(), ["__RequestVerificationToken"] = Token().Match(itemHtml).Groups[1].Value }));
        Assert.Equal(HttpStatusCode.Redirect, valid.StatusCode); Assert.Equal(5.5m, (await db.StockItems.AsNoTracking().SingleAsync(x => x.Id == item.Id, Ct)).CurrentStock); Assert.True(await db.StockMovements.AnyAsync(x => x.StockItemId == item.Id && x.Reason == "Web test entry", Ct));
        var history = await doctor.GetStringAsync(valid.Headers.Location); Assert.Contains("Web test entry", history);

    }

    [Fact]
    public async Task ClinicalVisit_PersistsVitalsEscapesNotesAndLinksFilesWithoutChangingStock()
    {
        await using var factory = Factory(); using var nurse = Client(factory); await LoginAsync(nurse, "other");
        await using var db = fixture.Sql.Medical(); var movementCount = await db.StockMovements.CountAsync(Ct); var key = Guid.NewGuid();
        var html = await nurse.GetStringAsync("/ClinicalRecords/Create?personId=" + Number(fixture.Sql.EmployeeId));
        using var response = await nurse.PostAsync("/ClinicalRecords/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["PersonId"] = Number(fixture.Sql.EmployeeId),
            ["VisitedAt"] = SqlFixture.Clock().Local(DateTime.UtcNow.AddHours(-1)).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            ["RequestKey"] = key.ToString(),
            ["Reason"] = "Synthetic visit",
            ["Findings"] = "Synthetic findings",
            ["Diagnosis"] = "Test diagnosis",
            ["Treatment"] = "Test instructions",
            ["Prescription"] = "Test recorded prescription",
            ["Notes"] = "<script>clinical-test</script>",
            ["Weight"] = "72.25",
            ["Height"] = "170",
            ["Temperature"] = "36.5",
            ["Systolic"] = "120",
            ["Diastolic"] = "80",
            ["Pulse"] = "70",
            ["RespiratoryRate"] = "16",
            ["OxygenSaturation"] = "98",
            ["Glucose"] = "95.5",
            ["__RequestVerificationToken"] = Token().Match(html).Groups[1].Value
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); var visit = await db.ClinicalVisits.SingleAsync(x => x.RequestKey == key, Ct);
        Assert.Equal(72.25m, visit.Weight); Assert.Equal(170m, visit.Height); Assert.Equal(36.5m, visit.Temperature); Assert.Equal(120, visit.Systolic); Assert.Equal(80, visit.Diastolic); Assert.Equal(70, visit.Pulse); Assert.Equal(16, visit.RespiratoryRate); Assert.Equal(98, visit.OxygenSaturation); Assert.Equal(95.5m, visit.Glucose); Assert.Equal("Test recorded prescription", visit.Prescription);
        var record = await nurse.GetStringAsync("/ClinicalRecords?personId=" + Number(fixture.Sql.EmployeeId) + "&visitId=" + Number(visit.Id)); Assert.Contains("&lt;script&gt;clinical-test&lt;/script&gt;", record); Assert.DoesNotContain("<script>clinical-test</script>", record); Assert.Contains("name=\"ClinicalVisitId\" value=\"" + Number(visit.Id), record);
        var uploadKey = Guid.NewGuid(); using var form = new MultipartFormDataContent(); form.Add(new StringContent(Number(fixture.Sql.EmployeeId)), "PersonId"); form.Add(new StringContent(Number(visit.Id)), "ClinicalVisitId"); form.Add(new StringContent(uploadKey.ToString()), "RequestKey"); form.Add(new StringContent(Token().Match(record).Groups[1].Value), "__RequestVerificationToken"); form.Add(new ByteArrayContent(Pdf), "Files", "Synthetic-study.pdf");
        using var uploaded = await nurse.PostAsync("/ClinicalRecords/Upload", form); Assert.Equal(HttpStatusCode.Redirect, uploaded.StatusCode); var file = await db.PatientDocuments.SingleAsync(x => x.RequestKey == uploadKey, Ct); Assert.Equal(visit.Id, file.ClinicalVisitId); Assert.Equal(fixture.Sql.EmployeeId, file.PersonId); Assert.Equal(movementCount, await db.StockMovements.CountAsync(Ct));
        var search = await nurse.GetStringAsync("/ClinicalRecords/People?term=TEST-E1"); Assert.Contains(Number(fixture.Sql.EmployeeId), search);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClinicalVisit_RejectsFutureDateAndInvertedPressure(bool future)
    {
        await using var factory = Factory(); using var doctor = Client(factory); await LoginAsync(doctor, "doctor"); var key = Guid.NewGuid();
        var html = await doctor.GetStringAsync("/ClinicalRecords/Create?personId=" + Number(fixture.Sql.EmployeeId));
        var values = new Dictionary<string, string> { ["PersonId"] = Number(fixture.Sql.EmployeeId), ["VisitedAt"] = SqlFixture.Clock().Local(DateTime.UtcNow.AddDays(future ? 1 : -1)).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture), ["RequestKey"] = key.ToString(), ["__RequestVerificationToken"] = Token().Match(html).Groups[1].Value };
        if (!future) { values["Systolic"] = "80"; values["Diastolic"] = "120"; }
        using var response = await doctor.PostAsync("/ClinicalRecords/Create", new FormUrlEncodedContent(values)); Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode); await using var db = fixture.Sql.Medical(); Assert.False(await db.ClinicalVisits.AnyAsync(x => x.RequestKey == key, Ct));
    }
    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex Token();
}
