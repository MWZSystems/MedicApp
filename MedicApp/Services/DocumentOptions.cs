using System.ComponentModel.DataAnnotations;
namespace MedicApp.Services;

public class DocumentOptions
{
    [Range(1, 10)] public int MaxFileSizeMb { get; set; } = 10;
    [Range(1, 20)] public int MaxBatchSizeMb { get; set; } = 20;
    [Range(1, 5)] public int MaxFilesPerUpload { get; set; } = 5;
    [Range(1, 100)] public int MaxDocumentsPerLeave { get; set; } = 20;
}
