using MedicApp.Models.Entities;
using MedicApp.Services;
using Microsoft.Extensions.Options;
namespace MedicApp.Tests;

public class RulesTests
{
    private static readonly DateTime Start = new(2026, 10, 30, 8, 0, 0, DateTimeKind.Utc);
    [Theory]
    [InlineData(-10, 5, true)]
    [InlineData(5, 20, true)]
    [InlineData(-10, 20, true)]
    [InlineData(0, 15, true)]
    [InlineData(15, 30, false)]
    [InlineData(-15, 0, false)]
    public void Overlaps_Intervals_UsesHalfOpenBoundaries(int otherStart, int otherEnd, bool expected)
    {
        Assert.Equal(expected, MedicalRules.Overlaps(Start, Start.AddMinutes(15), Start.AddMinutes(otherStart), Start.AddMinutes(otherEnd)));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Interval_NonPositiveDuration_Rejects(int minutes) => Assert.Throws<MedicalRuleException>(() => MedicalRules.ValidateInterval(Start, Start.AddMinutes(minutes)));
    [Fact]
    public void Booking_AlignedSlot_Accepts()
    {
        MedicalRules.ValidateBooking(Start, Start.AddMinutes(15), Start.AddDays(-1), 10, new() { StartsAt = Start, EndsAt = Start.AddHours(1), SlotMinutes = 15 });
    }
    [Theory]
    [InlineData(1, 16)]
    [InlineData(0, 30)]
    [InlineData(55, 70)]
    public void Booking_InvalidSlot_Rejects(int start, int end) => Assert.Throws<MedicalRuleException>(() => MedicalRules.ValidateBooking(Start.AddMinutes(start), Start.AddMinutes(end), Start.AddDays(-1), 10, new() { StartsAt = Start, EndsAt = Start.AddHours(1), SlotMinutes = 15 }));
    [Fact]
    public void Booking_PastOrOutsideHorizon_Rejects()
    {
        var window = new AvailabilityWindow { StartsAt = Start, EndsAt = Start.AddHours(1) };
        Assert.Throws<MedicalRuleException>(() => MedicalRules.ValidateBooking(Start, Start.AddMinutes(15), Start, 10, window));
        Assert.Throws<MedicalRuleException>(() => MedicalRules.ValidateBooking(Start, Start.AddMinutes(15), Start.AddDays(-11), 10, window));
    }
    [Fact]
    public void Appointment_EmployeeCannotAttend_Rejects() => Assert.Throws<MedicalRuleException>(() => MedicalRules.ValidateAppointmentTransition(AppointmentStatus.Scheduled, AppointmentStatus.Attended, false, Start, Start.AddMinutes(1)));
    [Fact]
    public void Appointment_FutureAttendance_Rejects() => Assert.Throws<MedicalRuleException>(() => MedicalRules.ValidateAppointmentTransition(AppointmentStatus.Scheduled, AppointmentStatus.Attended, true, Start, Start.AddMinutes(-1)));
    [Fact]
    public void Appointment_CancelledIsTerminal_Rejects() => Assert.Throws<MedicalRuleException>(() => MedicalRules.ValidateAppointmentTransition(AppointmentStatus.Cancelled, AppointmentStatus.Scheduled, true, Start, Start));
    [Fact]
    public void Leave_ApprovedCanCancel_Accepts() => MedicalRules.ValidateLeaveTransition(LeaveStatus.Approved, LeaveStatus.Cancelled);
    [Fact]
    public void Leave_CancelledCannotApprove_Rejects() => Assert.Throws<MedicalRuleException>(() => MedicalRules.ValidateLeaveTransition(LeaveStatus.Cancelled, LeaveStatus.Approved));
    [Fact]
    public void Clock_LocalRoundTrip_PreservesTime()
    {
        var clock = new MedicalClock(Options.Create(new MedicalOptions()), TimeProvider.System);
        Assert.Equal(Start, clock.ToUtc(clock.Local(Start)));
    }
    [Fact]
    public void Clock_NonexistentOrAmbiguousTime_Rejects()
    {
        var clock = new MedicalClock(Options.Create(new MedicalOptions { TimeZoneId = "America/New_York" }), TimeProvider.System);
        Assert.Throws<MedicalRuleException>(() => clock.ToUtc(new DateTime(2026, 3, 8, 2, 30, 0)));
        Assert.Throws<MedicalRuleException>(() => clock.ToUtc(new DateTime(2026, 11, 1, 1, 30, 0)));
    }
}
