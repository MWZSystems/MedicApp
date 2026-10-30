using System.ComponentModel.DataAnnotations;
namespace MedicApp.Models.ViewModels;

public class ChangeForm
{
    [Range(1, int.MaxValue)] public int Id { get; set; }
    public int Status { get; set; }
    [MaxLength(500), Display(Name = "Reason")] public string Reason { get; set; } = "";
    [Required(ErrorMessage = "Required")] public string Version { get; set; } = "";
}
