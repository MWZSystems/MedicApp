namespace MedicApp.Models.ViewModels;

public record CalendarSlot(DateTime StartsAtLocal, int DurationMinutes)
{
    public string Label => $"{StartsAtLocal:HH:mm}–{StartsAtLocal.AddMinutes(DurationMinutes):HH:mm}";
}
