using System.ComponentModel.DataAnnotations;
namespace MedicApp.Models.ViewModels;

public class ConfigurationForm
{
    [Range(1, 365), Display(Name = "BookingHorizonDays")] public int BookingHorizonDays { get; set; } = 10;
    [Display(Name = "Open24Hours")] public bool Open24Hours { get; set; } = true;
    [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$"), Display(Name = "OpeningTime")] public string OpeningTime { get; set; } = "08:00";
    [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$"), Display(Name = "ClosingTime")] public string ClosingTime { get; set; } = "18:00";
    [Range(5, 120), Display(Name = "DefaultSlotMinutes")] public int DefaultSlotMinutes { get; set; } = 15;
    [Required] public string Revision { get; set; } = "";
}
