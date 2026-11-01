using System.ComponentModel.DataAnnotations;
namespace MedicApp.Services;

public class InventoryOptions
{
    [Range(1, 365), Display(Name = "ExpiryWarningDays")] public int ExpiryWarningDays { get; set; } = 30;
}
