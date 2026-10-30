namespace MedicApp.Models.Entities;

public class LeaveDocument : MedicalRecord
{
    public int MedicalLeaveId { get; set; }
    public string StorageKey { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long ByteCount { get; set; }
    public string Sha256 { get; set; } = "";
    public Guid RequestKey { get; set; }
    public int Ordinal { get; set; }
}
