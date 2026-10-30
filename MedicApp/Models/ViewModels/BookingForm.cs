using System.ComponentModel.DataAnnotations;
namespace MedicApp.Models.ViewModels;

public class BookingForm
{
    [Range(1, int.MaxValue)] public int WindowId { get; set; }
    public DateTime StartsAt { get; set; }
    public Guid RequestKey { get; set; }
}
