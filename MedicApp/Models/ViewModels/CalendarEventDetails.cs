namespace MedicApp.Models.ViewModels;

public record CalendarEventDetails
{
    public string Kind { get; init; } = "appointment";
    public string Date { get; init; } = "";
    public int? AppointmentId { get; init; }
    public string Employee { get; init; } = "";
    public string Doctor { get; init; } = "";
    public string Status { get; init; } = "";
    public string Version { get; init; } = "";
    public bool CanCancel { get; init; }
    public bool CanAttend { get; init; }
}
