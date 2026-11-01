using System.ComponentModel.DataAnnotations;
namespace MedicApp.Services;

public class DocumentOptions : IValidatableObject
{
    [Display(Name = "MaxFileSizeMb"), Range(1, 10)] public int MaxFileSizeMb { get; set; } = 10;
    [Display(Name = "MaxBatchSizeMb"), Range(1, 20)] public int MaxBatchSizeMb { get; set; } = 20;
    [Display(Name = "MaxFilesPerUpload"), Range(1, 5)] public int MaxFilesPerUpload { get; set; } = 5;
    [Display(Name = "MaxDocumentsPerPatient"), Range(1, 1000)] public int MaxDocumentsPerPatient { get; set; } = 200;
    [Display(Name = "MaxDocumentsPerLeave"), Range(1, 100)] public int MaxDocumentsPerLeave { get; set; } = 20;
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MaxBatchSizeMb < MaxFileSizeMb) { yield return new("InvalidDocumentBatch", [nameof(MaxBatchSizeMb)]); }
        if (MaxDocumentsPerLeave < MaxFilesPerUpload) { yield return new("InvalidLeaveDocumentLimit", [nameof(MaxDocumentsPerLeave)]); }
    }
}
