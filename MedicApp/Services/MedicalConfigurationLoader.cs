using System.Text.Json;
using MedicApp.Models.ViewModels;
namespace MedicApp.Services;

public static class MedicalConfigurationLoader
{
    public static string FileName(string dataDirectory, IWebHostEnvironment environment) => Path.Combine(Path.GetFullPath(dataDirectory, environment.ContentRootPath), "Configuration", "medical.json");
    public static void Apply(MedicalOptions options, IWebHostEnvironment environment)
    {
        var file = FileName(options.DataDirectory, environment); if (!File.Exists(file)) { return; }
        using var document = JsonDocument.Parse(File.ReadAllText(file)); ApplyMedical(options, document.RootElement);
    }
    public static void ApplyMedical(MedicalOptions options, JsonElement root)
    {
        if (!root.TryGetProperty("Medical", out var medical)) { return; }
        var form = medical.Deserialize<ConfigurationForm>() ?? throw new InvalidOperationException("Invalid medical configuration.");
        options.BookingHorizonDays = form.BookingHorizonDays; options.Open24Hours = form.Open24Hours;
        options.OpeningTime = form.OpeningTime; options.ClosingTime = form.ClosingTime;
        options.DefaultSlotMinutes = form.DefaultSlotMinutes; options.DemoBookingEnabled = form.DemoBookingEnabled;
        if (medical.TryGetProperty("DisplayName", out var name)) { options.DisplayName = name.GetString() ?? throw new InvalidOperationException("Invalid display name."); }
        if (medical.TryGetProperty("TimeZoneId", out var zone)) { options.TimeZoneId = zone.GetString() ?? throw new InvalidOperationException("Invalid time zone."); }
    }
    public static void ApplyDocuments(DocumentOptions options, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var file = FileName(configuration["Medical:DataDirectory"] ?? "App_Data", environment); if (!File.Exists(file)) { return; }
        using var document = JsonDocument.Parse(File.ReadAllText(file)); ApplyDocuments(options, document.RootElement);
    }
    public static void ApplyDocuments(DocumentOptions options, JsonElement root)
    {
        if (!root.TryGetProperty("Documents", out var section)) { return; }
        var next = section.Deserialize<DocumentOptions>() ?? throw new InvalidOperationException("Invalid document configuration.");
        options.MaxFileSizeMb = next.MaxFileSizeMb; options.MaxBatchSizeMb = next.MaxBatchSizeMb; options.MaxFilesPerUpload = next.MaxFilesPerUpload;
        options.MaxDocumentsPerLeave = next.MaxDocumentsPerLeave; options.MaxDocumentsPerPatient = next.MaxDocumentsPerPatient;
    }
    public static void ApplyInventory(InventoryOptions options, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var file = FileName(configuration["Medical:DataDirectory"] ?? "App_Data", environment); if (!File.Exists(file)) { return; }
        using var document = JsonDocument.Parse(File.ReadAllText(file)); ApplyInventory(options, document.RootElement);
    }
    public static void ApplyInventory(InventoryOptions options, JsonElement root)
    {
        if (root.TryGetProperty("Inventory", out var section)) { options.ExpiryWarningDays = (section.Deserialize<InventoryOptions>() ?? throw new InvalidOperationException("Invalid inventory configuration.")).ExpiryWarningDays; }
    }
}
