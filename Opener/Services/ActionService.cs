using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Opener.Commands;
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
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Values to capture from this step's JSON response, as varName -&gt; simple path
    /// (dot-separated, with optional [index] array access, e.g. "data.token" or
    /// "items[0].id"). Captured values are available to later steps in a chain as
    /// {{varName}} in their Url, Headers values, or Body.
    /// </summary>
    public Dictionary<string, string>? Extract { get; set; }
}

/// <summary>
/// A Rest key's Value can be this shape instead of a single RestData - a login-then-call
/// flow where each step can extract values from its response for later steps to use.
/// </summary>
public class RestChainData
{
    public List<RestData>? Steps { get; set; }
}

[JsonSerializable(typeof(RestData))]
[JsonSerializable(typeof(RestChainData))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
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
    private readonly IHttpRequestSender _httpRequestSender;

    public ActionService(
        IConfigService? configService = null,
        IStorageService? storageService = null,
        IEmailService? emailService = null,
        ICalendarService? calendarService = null,
        IHttpRequestSender? httpRequestSender = null)
    {
        _configService = configService;
        _storageService = storageService;
        _emailService = emailService;
        _calendarService = calendarService;
        _httpRequestSender = httpRequestSender ?? new SystemHttpRequestSender();
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
            case OKeyType.Totp:
                HandleTotp(key, returnValue, forceCopy);
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

    private void HandleTotp(OKey key, bool returnValue = false, bool forceCopy = false)
    {
        string code;
        try
        {
            code = TotpService.GenerateCode(key.Value);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]TOTP Error:[/] {ex.Message}");
            return;
        }

        if (returnValue)
        {
            Console.WriteLine(code);
            return;
        }

        var validFor = (int)TotpService.TimeRemaining().TotalSeconds;
        TryCopyToClipboard(code, "TOTP code");
        AnsiConsole.MarkupLine($"[dim]Valid for ~{validFor}s[/]");
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
            var json = CommandHelpers.NormalizeJson(key.Value);
            var steps = ParseRestSteps(json);
            if (steps == null || steps.Count == 0)
            {
                return;
            }

            var conf = _configService?.GetConfig();
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string lastContent = string.Empty;

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var stepLabel = steps.Count > 1 ? $"({i + 1}/{steps.Count}) " : string.Empty;

                var resolved = UrlTemplateResolver.Resolve(
                    SubstituteVars(step.Url, vars),
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
                var method = string.IsNullOrWhiteSpace(step.Method) ? "GET" : step.Method;
                AnsiConsole.MarkupLine($"{stepLabel}[blue]{method}[/] {url}");

                var request = new HttpRequestMessage(new HttpMethod(method), url);

                string? contentType = null;
                if (step.Headers != null)
                {
                    foreach (var (headerName, headerValue) in step.Headers)
                    {
                        var resolvedHeaderValue = SubstituteVars(headerValue, vars);
                        if (headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        {
                            contentType = resolvedHeaderValue;
                        }
                        else
                        {
                            request.Headers.TryAddWithoutValidation(headerName, resolvedHeaderValue);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(step.Body))
                {
                    request.Content = new StringContent(SubstituteVars(step.Body, vars), Encoding.UTF8, contentType ?? "application/json");
                }

                var result = await _httpRequestSender.SendAsync(request);
                lastContent = result.Body;

                AnsiConsole.MarkupLine($"{stepLabel}Status: {result.StatusCode}");

                bool isLastStep = i == steps.Count - 1;
                if (!result.IsSuccess && !isLastStep)
                {
                    AnsiConsole.MarkupLine($"[red]Chain aborted:[/] step {i + 1} failed with status {result.StatusCode}.");
                    return;
                }

                if (step.Extract != null)
                {
                    foreach (var (varName, path) in step.Extract)
                    {
                        var value = JsonPathExtractor.Extract(result.Body, path);
                        if (value == null)
                        {
                            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not extract '{varName}' using path '{path}' from step {i + 1}'s response.");
                        }
                        else
                        {
                            vars[varName] = value;
                        }
                    }
                }
            }

            try { AnsiConsole.Write(new JsonText(lastContent)); }
            catch { AnsiConsole.WriteLine(lastContent); }
        }
        catch(Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Rest Error:[/] {ex.Message}");
        }
    }

    /// <summary>
    /// A Rest key's Value is either a single request (unchanged shape) or a { "steps": [...] }
    /// chain - detected by peeking for a top-level "steps" array so both shapes can share one
    /// execution path.
    /// </summary>
    private static List<RestData>? ParseRestSteps(string json)
    {
        using var probe = JsonDocument.Parse(json);
        bool isChain = probe.RootElement.ValueKind == JsonValueKind.Object &&
            probe.RootElement.TryGetProperty("steps", out var stepsElement) &&
            stepsElement.ValueKind == JsonValueKind.Array;

        if (isChain)
        {
            var chain = JsonSerializer.Deserialize(json, RestDataContext.Default.RestChainData);
            return chain?.Steps;
        }

        var single = JsonSerializer.Deserialize(json, RestDataContext.Default.RestData);
        return single == null ? null : new List<RestData> { single };
    }

    private static string SubstituteVars(string? input, Dictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(input) || vars.Count == 0)
        {
            return input ?? string.Empty;
        }

        foreach (var (name, value) in vars)
        {
            input = input.Replace("{{" + name + "}}", value);
        }
        return input;
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
            var json = CommandHelpers.NormalizeJson(key.Value);
            var template = JsonSerializer.Deserialize(json, OpenerJsonContext.Default.EmailTemplateData);
            if (template == null) return;

            var conf = _configService?.GetConfig();

            var resolvedTo = UrlTemplateResolver.Resolve(template.To, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;
            var resolvedCc = UrlTemplateResolver.Resolve(template.Cc, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;
            var resolvedBcc = UrlTemplateResolver.Resolve(template.Bcc, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;
            var resolvedSubject = UrlTemplateResolver.Resolve(template.Subject, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;
            var resolvedBody = UrlTemplateResolver.Resolve(template.Body, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;
            var resolvedAttachmentPath = UrlTemplateResolver.Resolve(template.AttachmentPath, args, conf?.GlobalUrlAliases, conf?.GlobalDefaultParams, key.UrlAliases, key.DefaultParams).Value;

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
                AttachmentPath = resolvedAttachmentPath,
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
            var json = CommandHelpers.NormalizeJson(key.Value);
            var eventData = JsonSerializer.Deserialize(json, OpenerJsonContext.Default.CalendarEventData);
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
