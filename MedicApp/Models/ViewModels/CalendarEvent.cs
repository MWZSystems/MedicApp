namespace MedicApp.Models.ViewModels;

public record CalendarEvent(string Id, string Title, string Start, string? End, bool AllDay, string Color, string[] ClassNames, CalendarEventDetails ExtendedProps);
