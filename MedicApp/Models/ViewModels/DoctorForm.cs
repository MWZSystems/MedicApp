using System.ComponentModel.DataAnnotations;
namespace MedicApp.Models.ViewModels;

public class DoctorForm
{
    public int Id { get; set; }
    [Range(1, int.MaxValue), Display(Name = "Employee")] public int PersonId { get; set; }
    [Required(ErrorMessage = "Required"), MaxLength(120), Display(Name = "Specialty")] public string Specialty { get; set; } = "";
    [Required(ErrorMessage = "Required"), MaxLength(60), Display(Name = "License")] public string ProfessionalLicense { get; set; } = "";
    public bool Available { get; set; } = true;
    public string Version { get; set; } = "";
}
