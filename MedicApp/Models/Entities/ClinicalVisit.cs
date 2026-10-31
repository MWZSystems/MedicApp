namespace MedicApp.Models.Entities;

public class ClinicalVisit : MedicalRecord
{
    public int PersonId { get; set; }
    public DateTime VisitedAt { get; set; }
    public Guid RequestKey { get; set; }
    public string Reason { get; set; } = "";
    public string Findings { get; set; } = "";
    public string Diagnosis { get; set; } = "";
    public string Treatment { get; set; } = "";
    public string Prescription { get; set; } = "";
    public string Notes { get; set; } = "";
    public decimal? Weight { get; set; }
    public decimal? Height { get; set; }
    public decimal? Temperature { get; set; }
    public int? Systolic { get; set; }
    public int? Diastolic { get; set; }
    public int? Pulse { get; set; }
    public int? RespiratoryRate { get; set; }
    public int? OxygenSaturation { get; set; }
    public decimal? Glucose { get; set; }
}
