using System.ComponentModel.DataAnnotations;
namespace MedicApp.Models.ViewModels;

public class WindowForm
{
    [Required, Display(Name = "Start")] public DateTime StartsAt { get; set; } = default;
    [Required, Display(Name = "End")] public DateTime EndsAt { get; set; } = default;
    [Range(5, 120), Display(Name = "SlotMinutes")] public int SlotMinutes { get; set; } = 15;
}
