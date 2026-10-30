namespace MedicApp.Models.Entities;

public class MedicalLeave : MedicalRecord
{
    public int PersonId { get; set; }
    public int DoctorId { get; set; }
    public int? AppointmentId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Draft;
    public string Reference { get; set; } = "";
    public string? CancellationReason { get; set; }
    public int Revision { get; set; } = 1;
}
