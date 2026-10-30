using System.ComponentModel.DataAnnotations;
namespace MedicApp.Models.ViewModels;

public class ReserveForm
{
    [Required(ErrorMessage = "Required")] public DateOnly? Date { get; set; }
    [Required(ErrorMessage = "Required")] public TimeOnly? Time { get; set; }
    public Guid RequestKey { get; set; }
}
