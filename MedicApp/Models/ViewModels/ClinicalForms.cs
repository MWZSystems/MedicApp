using System.ComponentModel.DataAnnotations;
using MedicApp.Models.Entities;
using MedicApp.Services;
namespace MedicApp.Models.ViewModels;

public class ClinicalVisitForm
{
    [Range(1, int.MaxValue)] public int PersonId { get; set; }
    public Guid RequestKey { get; set; } = Guid.NewGuid();
    [Required(ErrorMessage = "Required")] public DateTime VisitedAt { get; set; }
    [MaxLength(1000)] public string? Reason { get; set; }
    [MaxLength(4000)] public string? Findings { get; set; }
    [MaxLength(2000)] public string? Diagnosis { get; set; }
    [MaxLength(4000)] public string? Treatment { get; set; }
    [MaxLength(4000)] public string? Prescription { get; set; }
    [MaxLength(4000)] public string? Notes { get; set; }
    [Range(typeof(decimal), "0.1", "700")] public decimal? Weight { get; set; }
    [Range(typeof(decimal), "1", "300")] public decimal? Height { get; set; }
    [Range(typeof(decimal), "20", "50")] public decimal? Temperature { get; set; }
    [Range(20, 300)] public int? Systolic { get; set; }
    [Range(10, 200)] public int? Diastolic { get; set; }
    [Range(1, 300)] public int? Pulse { get; set; }
    [Range(1, 150)] public int? RespiratoryRate { get; set; }
    [Range(1, 100)] public int? OxygenSaturation { get; set; }
    [Range(typeof(decimal), "1", "1500")] public decimal? Glucose { get; set; }
}
public class PatientUploadForm
{
    [Range(1, int.MaxValue)] public int PersonId { get; set; }
    public int? ClinicalVisitId { get; set; }
    public Guid RequestKey { get; set; } = Guid.NewGuid();
    [EnumDataType(typeof(PatientDocumentKind))] public PatientDocumentKind Kind { get; set; }
    [MaxLength(1000)] public string? Description { get; set; }
    public List<IFormFile> Files { get; set; } = [];
}
public class ClinicalRecordPage
{
    public ClinicalVisit? SelectedVisit { get; set; }
    public Dictionary<string, string> AccountNames { get; set; } = [];
    public LeaveCalendarPerson? Person { get; set; }
    public ListPage<ClinicalVisit> Visits { get; set; } = new();
    public ListPage<PatientDocument> Documents { get; set; } = new();
    public DocumentOptions Limits { get; set; } = new();
}
public class StockItemForm
{
    [Required(ErrorMessage = "Required"), MaxLength(80)] public string Code { get; set; } = "";
    [Required(ErrorMessage = "Required"), MaxLength(180)] public string Name { get; set; } = "";
    [Required(ErrorMessage = "Required"), MaxLength(40)] public string Unit { get; set; } = "";
    [MaxLength(80)] public string? Lot { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    [Range(typeof(decimal), "0", "999999999")] public decimal MinimumStock { get; set; }
}
public class StockMovementForm
{
    [Range(1, int.MaxValue)] public int StockItemId { get; set; }
    public Guid RequestKey { get; set; } = Guid.NewGuid();
    [EnumDataType(typeof(StockMovementKind))] public StockMovementKind Kind { get; set; }
    [Range(typeof(decimal), "0.001", "999999999")] public decimal Quantity { get; set; }
    [Required(ErrorMessage = "Required"), MaxLength(1000)] public string Reason { get; set; } = "";
}
public class StockItemPage
{
    public Dictionary<string, string> AccountNames { get; set; } = [];
    public StockItem Item { get; set; } = new();
    public StockMovementForm Form { get; set; } = new();
    public ListPage<StockMovement> Movements { get; set; } = new();
}
