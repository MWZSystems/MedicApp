namespace MedicApp.Models.Entities;

public class AccessProfile : MedicalRecord
{
    public string UserId { get; set; } = "";
    public bool ManageDoctors { get; set; }
    public bool ManageAccess { get; set; }
    public bool ReadIntegration { get; set; }
}
