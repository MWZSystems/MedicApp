using System.ComponentModel.DataAnnotations;
namespace MedicApp.Services;

public class MedicalOptions
{
    [Required] public string DisplayName { get; set; } = "MedicApp";
    [Required] public string TimeZoneId { get; set; } = "America/Chihuahua";
    [Range(1, 365)] public int BookingHorizonDays { get; set; } = 10;
    [Required] public string DataDirectory { get; set; } = "App_Data";
}
