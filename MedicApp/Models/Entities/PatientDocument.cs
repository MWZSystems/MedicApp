namespace MedicApp.Models.Entities;

public enum PatientDocumentKind { General, Prescription, Study, Medication, Other }
public class PatientDocument : MedicalRecord
{
    public int PersonId { get; set; }
    public int? ClinicalVisitId { get; set; }
    public PatientDocumentKind Kind { get; set; }
    public string Description { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long ByteCount { get; set; }
    public string Sha256 { get; set; } = "";
    public Guid RequestKey { get; set; }
    public int Ordinal { get; set; }
}
