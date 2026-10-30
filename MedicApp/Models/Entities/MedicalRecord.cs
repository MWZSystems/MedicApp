namespace MedicApp.Models.Entities;

public abstract class MedicalRecord
{
    public int Id { get; set; }
    public bool Available { get; set; } = true;
    public string UpdatedById { get; set; } = "";
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
