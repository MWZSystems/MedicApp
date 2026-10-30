namespace MedicApp.Models.Entities;

public class AuditEntry : MedicalRecord
{
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public string Operation { get; set; } = "";
    public string Reason { get; set; } = "";
}
