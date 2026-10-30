using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using MedicApp.Models.ViewModels;
using Microsoft.Extensions.Options;
namespace MedicApp.Services;

public sealed class ApplicationConfiguration(IOptions<MedicalOptions> options, IWebHostEnvironment environment, MedicalService medical, MedicApp.Data.MedicDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private string FileName => Path.Combine(Path.GetFullPath(options.Value.DataDirectory, environment.ContentRootPath), "Configuration", "medical.json");
    private async Task<string> RevisionAsync(CancellationToken ct) => File.Exists(FileName) ? Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(FileName, ct))) : "default";
    public async Task<ConfigurationForm> ReadAsync(CancellationToken ct) => new()
    {
        BookingHorizonDays = options.Value.BookingHorizonDays,
        Open24Hours = options.Value.Open24Hours,
        OpeningTime = options.Value.OpeningTime,
        ClosingTime = options.Value.ClosingTime,
        DefaultSlotMinutes = options.Value.DefaultSlotMinutes,
        DemoBookingEnabled = options.Value.DemoBookingEnabled,
        Revision = await RevisionAsync(ct)
    };
    public async Task SaveAsync(ConfigurationForm form, CancellationToken ct)
    {
        var next = new MedicalOptions { BookingHorizonDays = form.BookingHorizonDays, Open24Hours = form.Open24Hours, OpeningTime = form.OpeningTime, ClosingTime = form.ClosingTime, DefaultSlotMinutes = form.DefaultSlotMinutes, DemoBookingEnabled = form.DemoBookingEnabled };
        if (!Validator.TryValidateObject(next, new ValidationContext(next), [], true)) { throw new MedicalRuleException("InvalidHours"); }
        await Gate.WaitAsync(ct);
        try
        {
            if (form.Revision != await RevisionAsync(ct)) { throw new MedicalRuleException("Concurrency"); }
            var contents = JsonSerializer.Serialize(new { Medical = new { next.BookingHorizonDays, next.Open24Hours, next.OpeningTime, next.ClosingTime, next.DefaultSlotMinutes, next.DemoBookingEnabled } }, JsonOptions);
            Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
            var temporary = FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var previous = File.Exists(FileName) ? await File.ReadAllTextAsync(FileName, ct) : null;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            medical.Audit("ApplicationConfiguration", 0, "Configure", contents);
            await db.SaveChangesAsync(ct);
            try
            {
                await File.WriteAllTextAsync(temporary, contents, ct);
                File.Move(temporary, FileName, true);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                if (previous == null) { if (File.Exists(FileName)) { File.Delete(FileName); } }
                else { await File.WriteAllTextAsync(temporary, previous, CancellationToken.None); File.Move(temporary, FileName, true); }
                throw;
            }
            finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
        }
        finally { Gate.Release(); }
    }
}
