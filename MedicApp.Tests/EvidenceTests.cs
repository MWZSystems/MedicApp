using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MedicApp.Data;
using MedicApp.Models.Entities;
using MedicApp.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Trainify.Domain;
namespace MedicApp.Tests;

public sealed class EvidenceFixture : IAsyncLifetime
{
    public SqlFixture Sql { get; } = new();
    public int OtherDoctor { get; private set; }
    public async ValueTask InitializeAsync()
    {
        await Sql.InitializeAsync();
        await using var shared = Sql.Shared();
        foreach (var name in new[] { "records-admin", "outsider" })
        {
            var person = new Person { EmployeeNumber = "TEST-" + name, FirstName = "Synthetic " + name };
            shared.People.Add(person); await shared.SaveChangesAsync();
            var user = new AppUser { Id = name, UserName = name, NormalizedUserName = name.ToUpperInvariant(), PersonId = person.Id, MustChangePassword = false, SecurityStamp = Guid.NewGuid().ToString() };
            user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, Sql.Password); shared.Users.Add(user);
            shared.LoginIdentifiers.Add(new() { Key = name.ToUpperInvariant(), UserId = name }); await shared.SaveChangesAsync();
        }
        await using var db = Sql.Medical();
        db.AccessProfiles.Add(new() { UserId = "records-admin", ManageDoctors = true, UpdatedById = "doctor" });
        var doctor = new Doctor { PersonId = Sql.OtherId, Specialty = "Test", ProfessionalLicense = "OTHER-TEST", UpdatedById = "doctor" };
        db.Doctors.Add(doctor); await db.SaveChangesAsync(); OtherDoctor = doctor.Id;
    }
    public ValueTask DisposeAsync() => Sql.DisposeAsync();
}
[CollectionDefinition("EvidenceSql", DisableParallelization = true)]
public class EvidenceGroup : ICollectionFixture<EvidenceFixture>;
[Collection("EvidenceSql")]
public partial class EvidenceTests(EvidenceFixture fixture) : IAsyncDisposable
{
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF");
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j4N8AAAAASUVORK5CYII=");
    private readonly string data = Path.Combine(Path.GetTempPath(), "MedicAppEvidenceTests", Guid.NewGuid().ToString("N"));
    public ValueTask DisposeAsync()
    {
        var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MedicAppEvidenceTests"));
        var actual = Path.GetFullPath(data);
        if (!string.Equals(Path.GetDirectoryName(actual), expected, StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException("Unsafe test cleanup"); }
        if (Directory.Exists(actual)) { Directory.Delete(actual, true); }
        GC.SuppressFinalize(this); return ValueTask.CompletedTask;
    }
    private WebApplicationFactory<Program> Factory(bool oneDocument = false) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Workforce"] = fixture.Sql.Connection,
            ["Medical:DataDirectory"] = data,
            ["AllowedHosts"] = "localhost",
            ["Documents:MaxDocumentsPerLeave"] = oneDocument ? "1" : "20",
            ["Documents:MaxFilesPerUpload"] = oneDocument ? "1" : "5"
        }));
    });
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    private async Task LoginAsync(HttpClient client, string name)
    {
        var html = await client.GetStringAsync("/Account/Login");
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Identifier"] = name, ["Password"] = fixture.Sql.Password, ["__RequestVerificationToken"] = Token().Match(html).Groups[1].Value }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private async Task<int> DraftAsync()
    {
        await using var db = fixture.Sql.Medical(); await using var shared = fixture.Sql.Shared();
        var start = SqlFixture.Clock().Local(fixture.Sql.Tomorrow);
        return await SqlFixture.Service(db, shared, "doctor", fixture.Sql.DoctorPersonId).CreateLeaveAsync(fixture.Sql.EmployeeId, start, start.AddDays(1), "EVIDENCE-" + Guid.NewGuid().ToString("N"), null, TestContext.Current.CancellationToken);
    }
    private static MultipartFormDataContent Form(int id, Guid key, string token, params (string Name, byte[] Bytes)[] files)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(id.ToString(CultureInfo.InvariantCulture)), "LeaveId"); form.Add(new StringContent(key.ToString()), "RequestKey"); form.Add(new StringContent(token), "__RequestVerificationToken");
        foreach (var file in files) { var content = new ByteArrayContent(file.Bytes); content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream"); form.Add(content, "Files", file.Name); }
        return form;
    }
    private static async Task<string> UploadTokenAsync(HttpClient client, int id) => Token().Match(await client.GetStringAsync("/LeaveDocuments?leaveId=" + id.ToString(CultureInfo.InvariantCulture))).Groups[1].Value;
    private async Task<int> UploadPdfAsync(HttpClient client, int id)
    {
        using var form = Form(id, Guid.NewGuid(), await UploadTokenAsync(client, id), ("IMSS-test.pdf", Pdf));
        using var uploaded = await client.PostAsync("/LeaveDocuments/Upload", form); Assert.Equal(HttpStatusCode.Redirect, uploaded.StatusCode);
        await using var db = fixture.Sql.Medical(); return await db.LeaveDocuments.Where(x => x.MedicalLeaveId == id).OrderByDescending(x => x.Id).Select(x => x.Id).FirstAsync(TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task Upload_RoundTripEncryptedAuditedIdempotentAndVisibleInPatientFile()
    {
        var id = await DraftAsync(); await using var factory = Factory(); using var client = Client(factory); await LoginAsync(client, "doctor");
        var key = Guid.NewGuid(); var token = await UploadTokenAsync(client, id);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var form = Form(id, key, token, ("IMSS-test.pdf", Pdf), ("scan.png", Png));
            using var response = await client.PostAsync("/LeaveDocuments/Upload", form); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        await using var db = fixture.Sql.Medical(); var documents = await db.LeaveDocuments.AsNoTracking().Where(x => x.MedicalLeaveId == id).OrderBy(x => x.Ordinal).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, documents.Count); Assert.Equal("application/pdf", documents[0].ContentType);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(data, "Evidence"), "*.dat", SearchOption.AllDirectories).Length);
        var encrypted = await File.ReadAllBytesAsync(Path.Combine(data, "Evidence", documents[0].StorageKey[..2], documents[0].StorageKey + ".dat"), TestContext.Current.CancellationToken);
        Assert.NotEqual(Pdf, encrypted); Assert.DoesNotContain("%PDF-", Encoding.ASCII.GetString(encrypted));
        using var download = await client.GetAsync("/LeaveDocuments/Download/" + documents[0].Id.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(HttpStatusCode.OK, download.StatusCode); Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(Pdf, await download.Content.ReadAsByteArrayAsync()); Assert.Contains("no-store", download.Headers.CacheControl?.ToString());
        var record = await client.GetStringAsync("/LeaveDocuments/Record?personId=" + fixture.Sql.EmployeeId.ToString(CultureInfo.InvariantCulture)); Assert.Contains("Expediente documental", record); Assert.Contains("Documentos (2)", record);
        var detail = await client.GetStringAsync("/LeaveDocuments?leaveId=" + id.ToString(CultureInfo.InvariantCulture)); Assert.Contains("IMSS-test.pdf", detail); Assert.DoesNotContain(documents[0].StorageKey, detail);
        Assert.Equal(2, await db.Audit.CountAsync(x => x.EntityType == "LeaveDocument" && x.Operation == "Upload" && documents.Select(d => d.Id).Contains(x.EntityId), TestContext.Current.CancellationToken));
        Assert.True(await db.Audit.AnyAsync(x => x.EntityType == "LeaveDocument" && x.EntityId == documents[0].Id && x.Operation == "Download", TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Permissions_OwnerCanReadNotUploadAndOtherDoctorOrEmployeeCannotRead()
    {
        var id = await DraftAsync(); await using var factory = Factory(); using var doctor = Client(factory); await LoginAsync(doctor, "doctor"); var doc = await UploadPdfAsync(doctor, id);
        using var owner = Client(factory); await LoginAsync(owner, "employee");
        var html = await owner.GetStringAsync("/LeaveDocuments?leaveId=" + id.ToString(CultureInfo.InvariantCulture)); Assert.DoesNotContain("id=\"evidence-files\"", html);
        using var allowed = await owner.GetAsync("/LeaveDocuments/Download/" + doc.ToString(CultureInfo.InvariantCulture)); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        var ownRegistry = await owner.GetStringAsync("/Leaves"); using var form = Form(id, Guid.NewGuid(), Token().Match(ownRegistry).Groups[1].Value, ("forbidden.pdf", Pdf));
        using var upload = await owner.PostAsync("/LeaveDocuments/Upload", form); Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        foreach (var name in new[] { "other", "outsider" })
        {
            using var stranger = Client(factory); await LoginAsync(stranger, name);
            using var denied = await stranger.GetAsync("/LeaveDocuments/Download/" + doc.ToString(CultureInfo.InvariantCulture)); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        using var anonymous = Client(factory); using var redirect = await anonymous.GetAsync("/LeaveDocuments/Download/" + doc.ToString(CultureInfo.InvariantCulture)); Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
    }
    [Fact]
    public async Task AdministrativeCapture_HasAddButtonAssignsDoctorAndCannotApprove()
    {
        await using var factory = Factory(); using var admin = Client(factory); await LoginAsync(admin, "records-admin");
        var registry = WebUtility.HtmlDecode(await admin.GetStringAsync("/Leaves")); Assert.Contains("Agregar incapacidad", registry);
        var capture = await admin.GetStringAsync("/Leaves/Create"); Assert.Contains("name=\"DoctorId\"", capture);
        var reference = "ADMIN-" + Guid.NewGuid().ToString("N"); var start = SqlFixture.Clock().Local(fixture.Sql.Tomorrow);
        using var created = await admin.PostAsync("/Leaves/Create", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["PersonId"] = fixture.Sql.EmployeeId.ToString(CultureInfo.InvariantCulture),
            ["DoctorId"] = fixture.Sql.DoctorId.ToString(CultureInfo.InvariantCulture),
            ["Reference"] = reference,
            ["StartsAt"] = start.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            ["EndsAt"] = start.AddDays(1).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            ["__RequestVerificationToken"] = Token().Match(capture).Groups[1].Value
        }));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode); Assert.Contains("LeaveDocuments", created.Headers.Location?.ToString());
        await using var db = fixture.Sql.Medical(); var row = await db.Leaves.SingleAsync(x => x.Reference == reference, TestContext.Current.CancellationToken); Assert.Equal(fixture.Sql.DoctorId, row.DoctorId); Assert.Equal(LeaveStatus.Draft, row.Status);
        await UploadPdfAsync(admin, row.Id);
        using var approve = await admin.PostAsync("/Leaves/Change", new FormUrlEncodedContent(new Dictionary<string, string> { ["Id"] = row.Id.ToString(CultureInfo.InvariantCulture), ["Status"] = "2", ["Reason"] = "Sin permiso clínico", ["Version"] = Convert.ToBase64String(row.RowVersion), ["__RequestVerificationToken"] = Token().Match(capture).Groups[1].Value }));
        Assert.Equal(HttpStatusCode.Redirect, approve.StatusCode); Assert.Contains("Denied", approve.Headers.Location?.ToString());
    }
    [Theory]
    [InlineData("bad.exe")]
    [InlineData("bad.svg")]
    [InlineData("bad.pdf")]
    public async Task InvalidBatch_DoesNotSaveMetadataOrPartialFiles(string badName)
    {
        var id = await DraftAsync(); await using var factory = Factory(); using var client = Client(factory); await LoginAsync(client, "doctor");
        using var form = Form(id, Guid.NewGuid(), await UploadTokenAsync(client, id), ("good.pdf", Pdf), (badName, Encoding.UTF8.GetBytes("<script>invalid</script>")));
        using var response = await client.PostAsync("/LeaveDocuments/Upload", form); Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await using var db = fixture.Sql.Medical(); Assert.False(await db.LeaveDocuments.AnyAsync(x => x.MedicalLeaveId == id, TestContext.Current.CancellationToken)); Assert.False(Directory.Exists(Path.Combine(data, "Evidence")));
    }
    [Fact]
    public async Task Upload_RequiresAntiforgeryAndEnforcesPerLeaveQuota()
    {
        var id = await DraftAsync(); await using var factory = Factory(true); using var client = Client(factory); await LoginAsync(client, "doctor");
        using (var forged = Form(id, Guid.NewGuid(), "", ("test.pdf", Pdf))) { using var response = await client.PostAsync("/LeaveDocuments/Upload", forged); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        await UploadPdfAsync(client, id);
        using var second = Form(id, Guid.NewGuid(), await UploadTokenAsync(client, id), ("second.pdf", Pdf)); using var blocked = await client.PostAsync("/LeaveDocuments/Upload", second); Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        await using var db = fixture.Sql.Medical(); Assert.Equal(1, await db.LeaveDocuments.CountAsync(x => x.MedicalLeaveId == id, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task ConcurrentRetry_CreatesOneDocumentAndOneEncryptedFile()
    {
        var id = await DraftAsync(); await using var factory = Factory(); using var client = Client(factory); await LoginAsync(client, "doctor");
        var key = Guid.NewGuid(); var token = await UploadTokenAsync(client, id);
        async Task UploadAsync() { using var form = Form(id, key, token, ("same.pdf", Pdf)); using var response = await client.PostAsync("/LeaveDocuments/Upload", form); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); }
        await Task.WhenAll(UploadAsync(), UploadAsync()); await using var db = fixture.Sql.Medical(); Assert.Equal(1, await db.LeaveDocuments.CountAsync(x => x.MedicalLeaveId == id, TestContext.Current.CancellationToken));
        Assert.Single(Directory.GetFiles(Path.Combine(data, "Evidence"), "*.dat", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task CorruptEncryptedFile_IsNotServed()
    {
        var id = await DraftAsync(); await using var factory = Factory(); using var client = Client(factory); await LoginAsync(client, "doctor"); var documentId = await UploadPdfAsync(client, id);
        var path = Assert.Single(Directory.GetFiles(Path.Combine(data, "Evidence"), "*.dat", SearchOption.AllDirectories));
        var bytes = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken); bytes[^1] ^= 1; await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
        using var response = await client.GetAsync("/LeaveDocuments/Download/" + documentId.ToString(CultureInfo.InvariantCulture)); Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
    private sealed class EnvironmentStub(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "MedicApp"; public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = root; public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
    private EvidenceStorage Storage(DocumentOptions? limits = null) => new(Options.Create(new MedicalOptions { DataDirectory = data }), Options.Create(limits ?? new DocumentOptions()), new EnvironmentStub(data), new EphemeralDataProtectionProvider());
    private static FormFile FormFileForTest(byte[] bytes, string name) => new(new MemoryStream(bytes), 0, bytes.Length, "Files", name);
    [Fact]
    public async Task Validation_RejectsOversizeAndSanitizesOriginalPaths()
    {
        var storage = Storage(new DocumentOptions { MaxFileSizeMb = 1, MaxBatchSizeMb = 1 });
        var tooLarge = new FormFile(new MemoryStream([1]), 0, 1048577, "Files", "big.pdf");
        await Assert.ThrowsAsync<MedicalRuleException>(() => storage.PrepareAsync([tooLarge], TestContext.Current.CancellationToken));
        var accepted = Assert.Single(await storage.PrepareAsync([FormFileForTest(Pdf, "../../outside/IMSS.pdf")], TestContext.Current.CancellationToken)); Assert.Equal("IMSS.pdf", accepted.OriginalName);
        var payload = new byte[600000]; Pdf.AsSpan(0, 5).CopyTo(payload); "%%EOF"u8.CopyTo(payload.AsSpan(payload.Length - 5));
        await Assert.ThrowsAsync<MedicalRuleException>(() => storage.PrepareAsync([FormFileForTest(payload, "one.pdf"), FormFileForTest(payload, "two.pdf")], TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task DatabaseFailure_CompensatesNewEncryptedFiles()
    {
        var id = await DraftAsync(); await using var db = fixture.Sql.Medical(); await using var shared = fixture.Sql.Shared();
        var access = new MedicalAccess(db, SqlFixture.Context("missing-user", fixture.Sql.DoctorPersonId));
        var medical = new MedicalService(db, shared, access, SqlFixture.Clock(), Options.Create(new MedicalOptions()));
        var service = new LeaveEvidenceService(db, new LeaveRecordAccess(db, access), access, medical, Storage(), Options.Create(new DocumentOptions()));
        await Assert.ThrowsAsync<DbUpdateException>(() => service.UploadAsync(id, Guid.NewGuid(), [FormFileForTest(Pdf, "rollback.pdf")], TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(Path.Combine(data, "Evidence"), "*.dat", SearchOption.AllDirectories)); Assert.False(await db.LeaveDocuments.AsNoTracking().AnyAsync(x => x.MedicalLeaveId == id, TestContext.Current.CancellationToken));
    }
    [GeneratedRegex(@"name=\x22__RequestVerificationToken\x22[^>]*value=\x22([^\x22]+)\x22")]
    private static partial Regex Token();
}
