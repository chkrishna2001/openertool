using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Opener.Models;
using Spectre.Console;
using Spectre.Console.Json;
using TextCopy;

namespace Opener.Services;

public class RestData
{
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string Body { get; set; } = string.Empty;
}

[JsonSerializable(typeof(RestData))]
public partial class RestDataContext : JsonSerializerContext { }

public interface IActionService
{
    Task ExecuteAsync(OKey key, string[] args, bool returnValue = false, bool forceCopy = false, bool elevated = false);
}

public class ActionService : IActionService
{
    private readonly IConfigService? _configService;
    private readonly IStorageService? _storageService;
    private readonly IEmailService? _emailService;
    private readonly ICalendarService? _calendarService;

    public ActionService(
        IConfigService? configService = null,
        IStorageService? storageService = null,
        IEmailService? emailService = null,
        ICalendarService? calendarService = null)
    {
        _configService = configService;
        _storageService = storageService;
        _emailService = emailService;
        _calendarService = calendarService;
    }

    public async Task ExecuteAsync(OKey key, string[] args, bool returnValue = false, bool forceCopy = false, bool elevated = false)
    {
        switch (key.KeyType)
        {
            case OKeyType.WebPath:
                HandleWebPath(key, args, returnValue, forceCopy);
                break;
            case OKeyType.LocalPath:
                HandleLocalPath(key, args, returnValue, forceCopy, elevated);
                break;
            case OKeyType.JsonData:
                HandleJsonData(key, returnValue, forceCopy);
                break;
            case OKeyType.Data:
                HandleData(key, returnValue, forceCopy);
                break;
            case OKeyType.Rest:
                await HandleRest(key, args);
                break;
            case OKeyType.EmailTemplate:
                await HandleEmailTemplate(key, args, returnValue, forceCopy);
                break;
            case OKeyType.CalendarEvent:
                await HandleCalendarEvent(key, args, returnValue, forceCopy);
                break;
            default:
                AnsiConsole.MarkupLine("[red]Unknown key type.[/]");
                break;
        }
    }

    private void HandleWebPath(OKey key, string[] args, bool returnValue = false, bool forceCopy = false)
    {
        var conf = _configService?.GetConfig();
        var resolved = UrlTemplateResolver.Resolve(
            key.Value,
            args,
            conf?.GlobalUrlAliases,
            conf?.GlobalDefaultParams,
            key.UrlAliases,
            key.DefaultParams);

        foreach (var warning in resolved.Warnings)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] {warning}");
        }

        if (returnValue)
        {
            Console.WriteLine(resolved.Value);
            return;
        }

        if (forceCopy)
        {
            TryCopyToClipboard(resolved.Value, "Value");
            return;
        }

        OpenUrl(resolved.Value);
    }

    private void HandleLocalPath(OKey key, string[] args, bool returnValue = false, bool forceCopy = false, bool elevated = false)
    {
        if (returnValue)
        {
            Console.WriteLine(key.Value);
            return;
        }

        if (forceCopy)
        {
            TryCopyToClipboard(key.Value, "Value");
            return;
        }

        bool isElevated = key.Elevated || elevated;
        ProcessStartInfo psi;

        if (isElevated)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                psi = new ProcessStartInfo
                {
                    FileName = key.Value,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                if (args != null) foreach (var arg in args) psi.ArgumentList.Add(arg);
            }
            else
            {
                // Linux and macOS: use sudo
                psi = new ProcessStartInfo
                {
                    FileName = "sudo",
                    UseShellExecute = false
                };
                psi.ArgumentList.Add(key.Value);
                if (args != null) foreach (var arg in args) psi.ArgumentList.Add(arg);
            }
        }
        else
        {
            psi = new ProcessStartInfo
            {
                FileName = key.Value,
                UseShellExecute = true
            };
            if (args != null) foreach (var arg in args) psi.ArgumentList.Add(arg);
        }

        try
        {
            Process.Start(psi);
        }
        catch
        {
            // Fallback for non-executable files or different platforms
            OpenUrl(key.Value);
        }
    }

    private void HandleJsonData(OKey key, bool returnValue = false, bool forceCopy = false)
    {
        if (returnValue)
        {
            Console.WriteLine(key.Value);
            return;
        }

        try 
        {
            using var doc = JsonDocument.Parse(key.Value);
            ClipboardService.SetText(key.Value);
            AnsiConsole.MarkupLine("[green]JSON Data copied to clipboard![/]");
            AnsiConsole.Write(new JsonText(key.Value));
        }
        catch
        {
             ClipboardService.SetText(key.Value);
             AnsiConsole.MarkupLine("[yellow]Value was not valid JSON, copied as plain text.[/]");
        }
    }
    private void HandleData(OKey key, bool returnValue = false, bool forceCopy = false)
    {
        if (returnValue)
        {
            Console.WriteLine(key.Value);
            return;
        }

        TryCopyToClipboard(key.Value, "Data");
    }

    private static void TryCopyToClipboard(string text, string label)
    {
        try
        {
            ClipboardService.SetText(text);
            WriteCopyConfirmation(label);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Unable to copy {label.ToLowerInvariant()} to clipboard:[/] {ex.Message}");
            WriteCopyConfirmation(label);
        }
    }

    private static void WriteCopyConfirmation(string label)
    {
        var message = $"{label} copied to clipboard!";
        AnsiConsole.MarkupLine($"[green]{message}[/]");
        Console.WriteLine(message);
    }

    private async Task HandleRest(OKey key, string[] args)
    {
        try
        {
            var restData = JsonSerializer.Deserialize(key.Value, RestDataContext.Default.RestData);
            if(restData == null) return;

            var conf = _configService?.GetConfig();
            var resolved = UrlTemplateResolver.Resolve(
                restData.Url,
                args,
                conf?.GlobalUrlAliases,
                conf?.GlobalDefaultParams,
                key.UrlAliases,
                key.DefaultParams);

            foreach (var warning in resolved.Warnings)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] {warning}");
            }

            var url = resolved.Value;

            AnsiConsole.MarkupLine($"[blue]{restData.Method}[/] {url}");

            using var client = new HttpClient();
            var request = new HttpRequestMessage(new HttpMethod(restData.Method), url);
            
            if(!string.IsNullOrEmpty(restData.Body))
            {
                request.Content = new StringContent(restData.Body, System.Text.Encoding.UTF8, "application/json");
            }

            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            AnsiConsole.MarkupLine($"Status: {response.StatusCode}");
            try { AnsiConsole.Write(new JsonText(content)); }
            catch { AnsiConsole.WriteLine(content); }
        }
        catch(Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Rest Error:[/] {ex.Message}");
        }
    }

    private async Task HandleEmailTemplate(OKey key, string[] args, bool returnValue = false, bool forceCopy = false)
    {
        if (_emailService == null)
        {
            AnsiConsole.MarkupLine("[red]EmailService is not initialized.[/]");
            return;
        }

        try
        {
            var template = JsonSerializer.Deserialize(key.Value, OpenerJsonContext.Default.EmailTemplateData);
            if (template == null) return;

            var conf = _configService?.GetConfig();

            var resolvedTo = UrlTemplateResolver.Resolve(template.To, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;
            var resolvedCc = UrlTemplateResolver.Resolve(template.Cc, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;
            var resolvedBcc = UrlTemplateResolver.Resolve(template.Bcc, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;
            var resolvedSubject = UrlTemplateResolver.Resolve(template.Subject, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;
            var resolvedBody = UrlTemplateResolver.Resolve(template.Body, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;

            if (returnValue)
            {
                Console.WriteLine(resolvedBody);
                return;
            }

            if (forceCopy)
            {
                TryCopyToClipboard(resolvedBody, "Email Body");
                return;
            }

            var resolvedTemplate = new EmailTemplateData
            {
                To = resolvedTo,
                Cc = resolvedCc,
                Bcc = resolvedBcc,
                Subject = resolvedSubject,
                Body = resolvedBody,
                Provider = template.Provider
            };

            await _emailService.SendEmailAsync(resolvedTemplate);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Email Template Error:[/] {ex.Message}");
        }
    }

    private async Task HandleCalendarEvent(OKey key, string[] args, bool returnValue = false, bool forceCopy = false)
    {
        if (_calendarService == null)
        {
            AnsiConsole.MarkupLine("[red]CalendarService is not initialized.[/]");
            return;
        }

        try
        {
            var eventData = JsonSerializer.Deserialize(key.Value, OpenerJsonContext.Default.CalendarEventData);
            if (eventData == null) return;

            var conf = _configService?.GetConfig();

            var resolvedSubject = UrlTemplateResolver.Resolve(eventData.Subject, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;
            var resolvedBody = UrlTemplateResolver.Resolve(eventData.Body, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;
            var resolvedInvitees = UrlTemplateResolver.Resolve(eventData.Invitees, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;
            var resolvedStartTime = UrlTemplateResolver.Resolve(eventData.StartTime, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;

            if (returnValue)
            {
                Console.WriteLine($"{resolvedSubject} ({resolvedStartTime}) - {resolvedBody}");
                return;
            }

            if (forceCopy)
            {
                TryCopyToClipboard(resolvedBody, "Event Body");
                return;
            }

            var resolvedEvent = new CalendarEventData
            {
                Subject = resolvedSubject,
                Body = resolvedBody,
                Invitees = resolvedInvitees,
                DurationMinutes = eventData.DurationMinutes,
                Availability = eventData.Availability,
                Provider = eventData.Provider,
                StartTime = resolvedStartTime
            };

            await _calendarService.CreateEventAsync(resolvedEvent);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Calendar Event Error:[/] {ex.Message}");
        }
    }

    private void OpenUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
        catch
        {
             AnsiConsole.MarkupLine($"[red]Could not open:[/] {url}");
        }
    }
}
