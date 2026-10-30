using System.ComponentModel.DataAnnotations;
namespace MedicApp.Models.ViewModels;

public class LeaveForm
{
    [Range(1, int.MaxValue), Display(Name = "Employee")] public int PersonId { get; set; }
    [Range(1, int.MaxValue), Display(Name = "ResponsibleDoctor")] public int? DoctorId { get; set; }
    [Display(Name = "Start")] public DateTime StartsAt { get; set; }
    [Display(Name = "End")] public DateTime EndsAt { get; set; }
    [Required(ErrorMessage = "Required"), MaxLength(80), Display(Name = "Reference")] public string Reference { get; set; } = "";
    [Range(1, int.MaxValue), Display(Name = "Appointment")] public int? AppointmentId { get; set; }
}
