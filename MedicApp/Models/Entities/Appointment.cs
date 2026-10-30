namespace MedicApp.Models.Entities;

public class Appointment : MedicalRecord
{
    public int PersonId { get; set; }
    public int DoctorId { get; set; }
    public int AvailabilityWindowId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public Guid RequestKey { get; set; }
    public string? CancellationReason { get; set; }
}
