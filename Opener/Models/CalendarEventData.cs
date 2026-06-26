namespace Opener.Models;

public class CalendarEventData
{
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Invitees { get; set; } = string.Empty; // Comma-separated emails
    public int DurationMinutes { get; set; } = 30;
    public string Availability { get; set; } = "busy"; // "free", "busy", "tentative", "oof"
    public string Provider { get; set; } = "system"; // "system", "graph"
    public string StartTime { get; set; } = string.Empty; // e.g. "tomorrow 10am" or placeholder
}
