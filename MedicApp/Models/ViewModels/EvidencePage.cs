using System.ComponentModel.DataAnnotations;
using MedicApp.Models.Entities;
using MedicApp.Services;
namespace MedicApp.Models.ViewModels;

public record EvidenceRow(int Id, string Name, string ContentType, long ByteCount, DateTime AddedAt);
public class EvidencePage
{
    public required MedicalLeave Leave { get; init; }
    public required string Person { get; init; }
    public bool CanUpload { get; init; }
    public IReadOnlyList<EvidenceRow> Documents { get; init; } = [];
    public required DocumentOptions Limits { get; init; }
}
public class EvidenceUploadForm
{
    [Range(1, int.MaxValue)] public int LeaveId { get; set; }
    public Guid RequestKey { get; set; }
    public List<IFormFile> Files { get; set; } = [];
}
public class PatientFilePage
{
    public LeaveCalendarPerson? Person { get; init; }
    public bool CanSearch { get; init; }
    public ListPage<MedicalLeave> Records { get; init; } = new();
    public Dictionary<int, int> DocumentCounts { get; init; } = [];
}
