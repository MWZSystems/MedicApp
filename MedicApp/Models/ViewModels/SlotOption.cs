namespace MedicApp.Models.ViewModels;

public record SlotOption(int WindowId, int DoctorId, string DoctorName, string Specialty, DateTime StartsAt, int Minutes);
