using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Opener.Models;
using Spectre.Console;

namespace Opener.Services;

public class GraphDateTimeTimeZone
{
    [JsonPropertyName("dateTime")]
    public string DateTime { get; set; } = string.Empty;

    [JsonPropertyName("timeZone")]
    public string TimeZone { get; set; } = "UTC";
}

public class GraphAttendee
{
    [JsonPropertyName("emailAddress")]
    public GraphEmailAddress EmailAddress { get; set; } = new();

    [JsonPropertyName("type")]
    public string Type { get; set; } = "required";
}

public class GraphLocation
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "Teams Meeting";
}

public class GraphEvent
{
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public GraphItemBody Body { get; set; } = new();

    [JsonPropertyName("start")]
    public GraphDateTimeTimeZone Start { get; set; } = new();

    [JsonPropertyName("end")]
    public GraphDateTimeTimeZone End { get; set; } = new();

    [JsonPropertyName("location")]
    public GraphLocation Location { get; set; } = new();

    [JsonPropertyName("attendees")]
    public List<GraphAttendee> Attendees { get; set; } = new();

    [JsonPropertyName("showAs")]
    public string ShowAs { get; set; } = "busy"; // free, tentative, busy, oof
}

[JsonSerializable(typeof(GraphEvent))]
internal partial class GraphEventJsonContext : JsonSerializerContext { }

public interface ICalendarService
{
    Task CreateEventAsync(CalendarEventData eventData);
}

public class CalendarService : ICalendarService
{
    private readonly IStorageService _storageService;
    private readonly IGraphAuthService _graphAuthService;

    public CalendarService(IStorageService storageService, IGraphAuthService graphAuthService)
    {
        _storageService = storageService;
        _graphAuthService = graphAuthService;
    }

    public async Task CreateEventAsync(CalendarEventData eventData)
    {
        var provider = eventData.Provider?.ToLowerInvariant() ?? "system";

        // Parse start and end times
        var start = ParseRelativeDateTime(eventData.StartTime, DateTime.Now);
        var end = start.AddMinutes(eventData.DurationMinutes > 0 ? eventData.DurationMinutes : 30);

        AnsiConsole.MarkupLine($"[yellow]Scheduling event: {eventData.Subject}[/]");
        AnsiConsole.MarkupLine($"[yellow]Time: {start:yyyy-MM-dd HH:mm} to {end:yyyy-MM-dd HH:mm}[/]");

        switch (provider)
        {
            case "system":
                CreateSystemEvent(eventData, start, end);
                break;
            case "graph":
                await CreateGraphEventAsync(eventData, start, end);
                break;
            default:
                AnsiConsole.MarkupLine($"[red]Unknown calendar provider '{provider}'. Defaulting to 'system' ICS creation.[/]");
                CreateSystemEvent(eventData, start, end);
                break;
        }
    }

    private void CreateSystemEvent(CalendarEventData eventData, DateTime start, DateTime end)
    {
        AnsiConsole.MarkupLine("[yellow]Generating iCalendar (.ics) file...[/]");
        
        var inviteeEmails = ParseEmailList(eventData.Invitees);
        var attendeesLines = string.Join("\r\n", inviteeEmails.Select(e => $"ATTENDEE;CN={e};RSVP=TRUE:mailto:{e}"));

        // ICS content uses UTC times in specific format: yyyyMMddTHHmmssZ
        var utcStart = start.ToUniversalTime();
        var utcEnd = end.ToUniversalTime();

        var icsContent = 
$@"BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Opener//CalendarEvent//EN
CALSCALE:GREGORIAN
METHOD:REQUEST
BEGIN:VEVENT
UID:{Guid.NewGuid():D}
DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}
DTSTART:{utcStart:yyyyMMddTHHmmssZ}
DTEND:{utcEnd:yyyyMMddTHHmmssZ}
SUMMARY:{eventData.Subject}
DESCRIPTION:{eventData.Body}
LOCATION:Online Meeting
STATUS:CONFIRMED
SEQUENCE:0
TRANSP:{(eventData.Availability?.Equals("free", StringComparison.OrdinalIgnoreCase) == true ? "TRANSPARENT" : "OPAQUE")}
{attendeesLines}
END:VEVENT
END:VCALENDAR";

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ics");
        File.WriteAllText(tempPath, icsContent);

        AnsiConsole.MarkupLine($"[yellow]Opening generated ICS file: {tempPath}[/]");

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", tempPath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", tempPath);
            }
            AnsiConsole.MarkupLine("[green]✔ System calendar event viewer opened![/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error opening ICS file:[/] {ex.Message}");
        }
    }

    private async Task CreateGraphEventAsync(CalendarEventData eventData, DateTime start, DateTime end)
    {
        AnsiConsole.MarkupLine("[yellow]Acquiring Microsoft Graph access token...[/]");
        var token = await _graphAuthService.GetAccessTokenAsync();

        if (string.IsNullOrEmpty(token))
        {
            throw new Exception("Not authenticated with Microsoft Graph. Run 'o config auth-graph' or set client credentials via 'o config set-provider graph'.");
        }

        AnsiConsole.MarkupLine("[yellow]Creating calendar event via Microsoft Graph API...[/]");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var inviteeEmails = ParseEmailList(eventData.Invitees);

        var payload = new GraphEvent
        {
            Subject = eventData.Subject,
            Body = new GraphItemBody
            {
                ContentType = "Text",
                Content = eventData.Body
            },
            Start = new GraphDateTimeTimeZone
            {
                DateTime = start.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeZone = "UTC"
            },
            End = new GraphDateTimeTimeZone
            {
                DateTime = end.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
                TimeZone = "UTC"
            },
            Attendees = inviteeEmails.Select(email => new GraphAttendee
            {
                EmailAddress = new GraphEmailAddress { Address = email },
                Type = "required"
            }).ToList(),
            ShowAs = eventData.Availability?.ToLowerInvariant() switch
            {
                "free" => "free",
                "tentative" => "tentative",
                "busy" => "busy",
                "oof" => "oof",
                _ => "busy"
            }
        };

        var jsonPayload = JsonSerializer.Serialize(payload, GraphEventJsonContext.Default.GraphEvent);
        var httpContent = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

        // Use /me/events or /users/{sender}/events for client credentials
        var keys = _storageService.GetKeys();
        var clientSecret = keys.Find(k => k.Key == "__provider_graph_client_secret")?.Value;

        string endpoint = "https://graph.microsoft.com/v1.0/me/events";

        if (!string.IsNullOrEmpty(clientSecret))
        {
            var senderEmail = keys.Find(k => k.Key == "__provider_smtp_username")?.Value;
            if (string.IsNullOrEmpty(senderEmail))
            {
                throw new Exception("Client credentials flow is active. Please define the calendar owner email by configuring SMTP username via 'o config set-provider smtp --username owner@company.com ...'.");
            }
            endpoint = $"https://graph.microsoft.com/v1.0/users/{senderEmail}/events";
        }

        var response = await client.PostAsync(endpoint, httpContent);

        if (response.IsSuccessStatusCode)
        {
            AnsiConsole.MarkupLine("[green]✔ Calendar event created successfully in Microsoft Outlook![/]");
        }
        else
        {
            var errContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Microsoft Graph API failed (Status: {response.StatusCode}): {errContent}");
        }
    }

    public static DateTime ParseRelativeDateTime(string input, DateTime relativeTo)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            // Default: Next top of the hour
            var nextHour = relativeTo.AddHours(1);
            return new DateTime(nextHour.Year, nextHour.Month, nextHour.Day, nextHour.Hour, 0, 0, DateTimeKind.Local);
        }

        var relativeKeywords = new[] { "today", "tomorrow", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday", "next" };
        bool isRelative = !string.IsNullOrWhiteSpace(input) && relativeKeywords.Any(k => input.Contains(k, StringComparison.OrdinalIgnoreCase));
        if (!isRelative && DateTime.TryParse(input, out var exactDate))
        {
            return exactDate;
        }

        // Relative parsing
        var text = input.ToLowerInvariant().Trim();
        var date = relativeTo.Date;
        var time = new TimeSpan(10, 0, 0); // Default to 10:00 AM

        var parts = text.Split(new[] { ' ', '@' }, StringSplitOptions.RemoveEmptyEntries);

        // Parse time first
        foreach (var part in parts)
        {
            if (TryParseTime(part, out var parsedTime))
            {
                time = parsedTime;
                break;
            }
        }

        // Parse date modifiers
        if (parts.Contains("tomorrow"))
        {
            date = date.AddDays(1);
        }
        else if (parts.Contains("today"))
        {
            // Keeps date = today
        }
        else if (text.Contains("next"))
        {
            foreach (DayOfWeek dayOfWeek in Enum.GetValues<DayOfWeek>())
            {
                if (text.Contains(dayOfWeek.ToString().ToLowerInvariant()))
                {
                    int daysToAdd = ((int)dayOfWeek - (int)date.DayOfWeek + 7) % 7;
                    if (daysToAdd == 0) daysToAdd = 7;
                    date = date.AddDays(daysToAdd);
                    break;
                }
            }
        }
        else
        {
            foreach (DayOfWeek dayOfWeek in Enum.GetValues<DayOfWeek>())
            {
                if (text.Contains(dayOfWeek.ToString().ToLowerInvariant()))
                {
                    int daysToAdd = ((int)dayOfWeek - (int)date.DayOfWeek + 7) % 7;
                    if (daysToAdd == 0) daysToAdd = 7;
                    date = date.AddDays(daysToAdd);
                    break;
                }
            }
        }

        return date.Add(time);
    }

    private static bool TryParseTime(string part, out TimeSpan time)
    {
        time = TimeSpan.Zero;
        var cleanPart = part.Trim().ToLowerInvariant();
        bool isPm = cleanPart.EndsWith("pm");
        bool isAm = cleanPart.EndsWith("am");

        if (isPm || isAm)
        {
            cleanPart = cleanPart.Substring(0, cleanPart.Length - 2).Trim();
        }

        if (cleanPart.Contains(':'))
        {
            if (TimeSpan.TryParse(cleanPart, out var tempTime))
            {
                var hours = tempTime.Hours;
                var minutes = tempTime.Minutes;

                if (isPm && hours < 12) hours += 12;
                if (isAm && hours == 12) hours = 0;

                time = new TimeSpan(hours, minutes, 0);
                return true;
            }
        }
        else
        {
            if (int.TryParse(cleanPart, out int hr))
            {
                if (hr >= 0 && hr <= 23)
                {
                    if (isPm && hr < 12) hr += 12;
                    if (isAm && hr == 12) hr = 0;

                    time = new TimeSpan(hr, 0, 0);
                    return true;
                }
            }
        }

        return false;
    }

    private static List<string> ParseEmailList(string emails)
    {
        if (string.IsNullOrWhiteSpace(emails)) return new List<string>();
        return emails.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(e => e.Trim())
                     .Where(e => !string.IsNullOrEmpty(e))
                     .ToList();
    }
}
