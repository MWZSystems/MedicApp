namespace MedicApp.Models.ViewModels;

public record LeaveCalendarPerson(int Id, string Name, string EmployeeNumber)
{
    public string Label => EmployeeNumber + " · " + Name;
}
public class LeaveCalendarPage
{
    public bool CanSearchPeople { get; init; }
    public LeaveCalendarPerson? Person { get; init; }
    public DateOnly FocusDate { get; init; }
}
public record LeaveCalendarDetails(string StartsAtLocal, string EndsAtLocal, string CoveredThrough);
public record LeaveCalendarEvent(string Id, string Title, string Start, string End, bool AllDay, string Color, LeaveCalendarDetails ExtendedProps);
