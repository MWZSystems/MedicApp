namespace MedicApp.Models.Entities;

public class Doctor : MedicalRecord
{
    public int PersonId { get; set; }
    public string Specialty { get; set; } = "";
    public string ProfessionalLicense { get; set; } = "";
}
