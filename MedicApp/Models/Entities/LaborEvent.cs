namespace MedicApp.Models.Entities;

public class LaborEvent : MedicalRecord
{
    public int MedicalLeaveId { get; set; }
    public int PersonId { get; set; }
    public int Revision { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public LeaveStatus Status { get; set; }
}
