using System.ComponentModel.DataAnnotations;
namespace MedicApp.Services;

public class GeneralOptions : IValidatableObject
{
    [Required(ErrorMessage = "Required"), MaxLength(80), Display(Name = "ApplicationDisplayName")] public string DisplayName { get; set; } = "MedicApp";
    [Required(ErrorMessage = "Required"), MaxLength(100), Display(Name = "ApplicationTimeZone")] public string TimeZoneId { get; set; } = "America/Chihuahua";
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Any(char.IsControl)) { yield return new("InvalidApplicationName", [nameof(DisplayName)]); }
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(TimeZoneId, out _)) { yield return new("InvalidApplicationTimeZone", [nameof(TimeZoneId)]); }
    }
}
