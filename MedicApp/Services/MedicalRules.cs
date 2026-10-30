using MedicApp.Models.Entities;
namespace MedicApp.Services;

public static class MedicalRules
{
    public static bool Overlaps(DateTime start, DateTime end, DateTime otherStart, DateTime otherEnd) => start < otherEnd && end > otherStart;
    public static void ValidateInterval(DateTime start, DateTime end)
    {
        if (end <= start) { throw new MedicalRuleException("InvalidInterval"); }
    }
    public static void ValidateBooking(DateTime start, DateTime end, DateTime now, int horizon, AvailabilityWindow window)
    {
        ValidateInterval(start, end);
        if (start <= now || start > now.AddDays(horizon)) { throw new MedicalRuleException("OutsideHorizon"); }
        if (window.SlotMinutes > 30 || !window.Available || start < window.StartsAt || end > window.EndsAt || (end - start).TotalMinutes != window.SlotMinutes || (start - window.StartsAt).TotalMinutes % window.SlotMinutes != 0)
        { throw new MedicalRuleException("UnavailableSlot"); }
    }
    public static void ValidateAppointmentTransition(AppointmentStatus current, AppointmentStatus next, bool doctor, DateTime startsAt, DateTime now)
    {
        if (current != AppointmentStatus.Scheduled || next == AppointmentStatus.Scheduled || !Enum.IsDefined(next)) { throw new MedicalRuleException("InvalidTransition"); }
        if (!doctor && (next != AppointmentStatus.Cancelled || startsAt <= now)) { throw new MedicalRuleException("Forbidden"); }
        if (next is AppointmentStatus.Attended or AppointmentStatus.NoShow && startsAt > now) { throw new MedicalRuleException("FutureAttendance"); }
    }
    public static void ValidateLeaveTransition(LeaveStatus current, LeaveStatus next)
    {
        if (!(current == LeaveStatus.Draft && next is LeaveStatus.Approved or LeaveStatus.Cancelled || current == LeaveStatus.Approved && next == LeaveStatus.Cancelled))
        { throw new MedicalRuleException("InvalidTransition"); }
    }
}
