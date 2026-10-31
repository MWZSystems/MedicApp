using System.ComponentModel.DataAnnotations;
namespace MedicApp.Models.ViewModels;

public class AccessForm
{
    [Required(ErrorMessage = "Required"), MaxLength(450)] public string UserId { get; set; } = "";
    public bool ManageDoctors { get; set; }
    public bool ManageAccess { get; set; }
    public bool ManageClinicalRecords { get; set; }
    public bool ManageInventory { get; set; }
    public bool ReadIntegration { get; set; }
    public string Version { get; set; } = "";
}
