using System.Text.Json;
using MedicApp.Models.ViewModels;
namespace MedicApp.Services;

public static class MedicalConfigurationLoader
{
    // Options are scoped: each new request reads the persistent scheduling override.
    public static void Apply(MedicalOptions options, IWebHostEnvironment environment)
    {
        var file = Path.Combine(Path.GetFullPath(options.DataDirectory, environment.ContentRootPath), "Configuration", "medical.json");
        if (!File.Exists(file)) { return; }
        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var form = document.RootElement.GetProperty("Medical").Deserialize<ConfigurationForm>() ?? throw new InvalidOperationException("Invalid medical configuration.");
        options.BookingHorizonDays = form.BookingHorizonDays;
        options.Open24Hours = form.Open24Hours;
        options.OpeningTime = form.OpeningTime;
        options.ClosingTime = form.ClosingTime;
        options.DefaultSlotMinutes = form.DefaultSlotMinutes;
        options.DemoBookingEnabled = form.DemoBookingEnabled;
    }
}
