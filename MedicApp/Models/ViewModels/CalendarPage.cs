namespace MedicApp.Models.ViewModels;

public class CalendarPage
{
    public DateOnly Date { get; set; }
    public DateOnly Today { get; set; }
    public DateOnly LastBookingDate { get; set; }
    public bool IsDoctor { get; set; }
    public bool DemoBookingEnabled { get; set; }
    public int BookingHorizonDays { get; set; }
    public int SlotMinutes { get; set; }
    public bool OpenBooking { get; set; }
    public IReadOnlyList<CalendarSlot> Slots { get; set; } = [];
}
