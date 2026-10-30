using System.ComponentModel.DataAnnotations;
using System.Globalization;
namespace MedicApp.Services;

public class MedicalOptions : IValidatableObject
{
    [Required] public string DisplayName { get; set; } = "MedicApp";
    [Required] public string TimeZoneId { get; set; } = "America/Chihuahua";
    [Range(1, 365), Display(Name = "BookingHorizonDays")] public int BookingHorizonDays { get; set; } = 10;
    [Display(Name = "Open24Hours")] public bool Open24Hours { get; set; } = true;
    [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$"), Display(Name = "OpeningTime")] public string OpeningTime { get; set; } = "08:00";
    [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$"), Display(Name = "ClosingTime")] public string ClosingTime { get; set; } = "18:00";
    [Range(5, 120), Display(Name = "DefaultSlotMinutes")] public int DefaultSlotMinutes { get; set; } = 15;
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Open24Hours && OpeningTime == ClosingTime) { yield return new ValidationResult("InvalidHours", [nameof(ClosingTime)]); }
    }
    public bool Allows(DateTime localStart, DateTime localEnd)
    {
        if (localEnd <= localStart) { return false; }
        if (Open24Hours) { return true; }
        var opening = TimeOnly.ParseExact(OpeningTime, "HH:mm", CultureInfo.InvariantCulture);
        var closing = TimeOnly.ParseExact(ClosingTime, "HH:mm", CultureInfo.InvariantCulture);
        var day = DateOnly.FromDateTime(localStart);
        if (closing < opening && TimeOnly.FromDateTime(localStart) < closing) { day = day.AddDays(-1); }
        var start = day.ToDateTime(opening);
        var end = day.AddDays(closing < opening ? 1 : 0).ToDateTime(closing);
        return localStart >= start && localEnd <= end;
    }
    [Required] public string DataDirectory { get; set; } = "App_Data";
}
