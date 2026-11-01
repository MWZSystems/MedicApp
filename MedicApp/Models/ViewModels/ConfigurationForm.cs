using System.ComponentModel.DataAnnotations;
using MedicApp.Services;
namespace MedicApp.Models.ViewModels;

public class ConfigurationForm
{
    public GeneralOptions? General { get; set; }
    public DocumentOptions? Documents { get; set; }
    public InventoryOptions? Inventory { get; set; }
    [Range(1, 365), Display(Name = "BookingHorizonDays")] public int BookingHorizonDays { get; set; } = 7;
    [Display(Name = "Open24Hours")] public bool Open24Hours { get; set; } = true;
    [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$"), Display(Name = "OpeningTime")] public string OpeningTime { get; set; } = "08:00";
    [Required, RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$"), Display(Name = "ClosingTime")] public string ClosingTime { get; set; } = "18:00";
    [Range(5, 30), Display(Name = "DefaultSlotMinutes")] public int DefaultSlotMinutes { get; set; } = 30;
    [Display(Name = "DemoBookingEnabled")] public bool DemoBookingEnabled { get; set; }
    [Required] public string Revision { get; set; } = "";
}
