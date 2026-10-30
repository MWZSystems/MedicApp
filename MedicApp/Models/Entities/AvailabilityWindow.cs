namespace MedicApp.Models.Entities;

public class AvailabilityWindow : MedicalRecord
{
    public bool Generated { get; set; }
    public int DoctorId { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int SlotMinutes { get; set; } = 15;
}
