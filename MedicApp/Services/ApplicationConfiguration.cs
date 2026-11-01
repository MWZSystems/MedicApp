using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using MedicApp.Models.ViewModels;
using Microsoft.Extensions.Options;
namespace MedicApp.Services;

public sealed class ApplicationConfiguration(IOptions<MedicalOptions> options, IOptions<DocumentOptions> documents, IOptions<InventoryOptions> inventory, IWebHostEnvironment environment, MedicalService medical, MedicApp.Data.MedicDbContext db, MedicalAccess access)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private string FileName => MedicalConfigurationLoader.FileName(options.Value.DataDirectory, environment);
    private static string Revision(byte[]? bytes) => bytes == null ? "default" : Convert.ToHexString(SHA256.HashData(bytes));
    private async Task<byte[]?> ContentsAsync(CancellationToken ct) => File.Exists(FileName) ? await File.ReadAllBytesAsync(FileName, ct) : null;
    private ConfigurationForm Snapshot(byte[]? bytes)
    {
        var current = JsonSerializer.Deserialize<MedicalOptions>(JsonSerializer.Serialize(options.Value)) ?? throw new InvalidOperationException("Missing medical options.");
        var files = JsonSerializer.Deserialize<DocumentOptions>(JsonSerializer.Serialize(documents.Value)) ?? throw new InvalidOperationException("Missing document options.");
        var stock = new InventoryOptions { ExpiryWarningDays = inventory.Value.ExpiryWarningDays };
        if (bytes != null)
        {
            using var document = JsonDocument.Parse(bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }) ? bytes.AsMemory(3) : bytes.AsMemory());
            MedicalConfigurationLoader.ApplyMedical(current, document.RootElement);
            MedicalConfigurationLoader.ApplyDocuments(files, document.RootElement);
            MedicalConfigurationLoader.ApplyInventory(stock, document.RootElement);
        }
        return new()
        {
            General = new() { DisplayName = current.DisplayName, TimeZoneId = current.TimeZoneId },
            BookingHorizonDays = current.BookingHorizonDays,
            Open24Hours = current.Open24Hours,
            OpeningTime = current.OpeningTime,
            ClosingTime = current.ClosingTime,
            DefaultSlotMinutes = current.DefaultSlotMinutes,
            DemoBookingEnabled = current.DemoBookingEnabled,
            Documents = files,
            Inventory = stock,
            Revision = Revision(bytes)
        };
    }
    public async Task<ConfigurationForm> ReadAsync(CancellationToken ct)
    {
        if (!await access.CanManageAccessAsync(ct)) { throw new MedicalRuleException("Forbidden"); }
        await Gate.WaitAsync(ct);
        try { return Snapshot(await ContentsAsync(ct)); }
        finally { Gate.Release(); }
    }
    private static void Validate(object value, string key)
    {
        if (!Validator.TryValidateObject(value, new ValidationContext(value), [], true)) { throw new MedicalRuleException(key); }
    }
    public async Task SaveAsync(ConfigurationForm form, CancellationToken ct)
    {
        if (!await access.CanManageAccessAsync(ct)) { throw new MedicalRuleException("Forbidden"); }
        Validate(form, "InvalidConfiguration");
        await Gate.WaitAsync(ct);
        try
        {
            var previous = await ContentsAsync(ct); var current = Snapshot(previous);
            if (form.Revision != current.Revision) { throw new MedicalRuleException("Concurrency"); }
            // Older clients submit scheduling only. Preserve the other persisted sections.
            form.General ??= current.General; form.Documents ??= current.Documents; form.Inventory ??= current.Inventory;
            var general = form.General ?? throw new MedicalRuleException("InvalidConfiguration");
            var files = form.Documents ?? throw new MedicalRuleException("InvalidConfiguration");
            var stock = form.Inventory ?? throw new MedicalRuleException("InvalidConfiguration");
            Validate(general, "InvalidGeneralConfiguration");
            general.DisplayName = general.DisplayName.Trim(); Validate(files, "InvalidDocumentConfiguration"); Validate(stock, "InvalidInventoryConfiguration");
            var next = new MedicalOptions { BookingHorizonDays = form.BookingHorizonDays, Open24Hours = form.Open24Hours, OpeningTime = form.OpeningTime, ClosingTime = form.ClosingTime, DefaultSlotMinutes = form.DefaultSlotMinutes, DemoBookingEnabled = form.DemoBookingEnabled };
            Validate(next, "InvalidHours");
            var payload = new { Medical = new { general.DisplayName, general.TimeZoneId, next.BookingHorizonDays, next.Open24Hours, next.OpeningTime, next.ClosingTime, next.DefaultSlotMinutes, next.DemoBookingEnabled }, Documents = files, Inventory = stock };
            var contents = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
            Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
            var temporary = FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            medical.Audit("ApplicationConfiguration", 0, "Configure", current.Revision + " -> " + Revision(contents));
            var before = Settings(current); var after = Settings(form);
            foreach (var (key, value) in after)
            {
                if (before.GetValueOrDefault(key) != value) { medical.Audit("ApplicationSetting", 0, "Change", key + ": " + before.GetValueOrDefault(key) + " -> " + value); }
            }
            await db.SaveChangesAsync(ct);
            try
            {
                await File.WriteAllBytesAsync(temporary, contents, ct); File.Move(temporary, FileName, true);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                if (previous == null) { if (File.Exists(FileName)) { File.Delete(FileName); } }
                else { await File.WriteAllBytesAsync(temporary, previous, CancellationToken.None); File.Move(temporary, FileName, true); }
                throw;
            }
            finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
        }
        finally { Gate.Release(); }
    }
    private static Dictionary<string, string> Settings(ConfigurationForm form)
    {
        var values = new Dictionary<string, string>();
        void Add(string prefix, object? value)
        {
            if (value == null) { return; }
            foreach (var property in value.GetType().GetProperties())
            {
                if (property.Name is "Revision" or "General" or "Documents" or "Inventory") { continue; }
                values[prefix + property.Name] = Convert.ToString(property.GetValue(value), CultureInfo.InvariantCulture) ?? "";
            }
        }
        Add("Medical.", form); Add("Medical.", form.General); Add("Documents.", form.Documents); Add("Inventory.", form.Inventory); return values;
    }
}
