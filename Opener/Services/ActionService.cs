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
    Task ExecuteAsync(OKey key, string[] args);
}

public class ActionService : IActionService
{
    private readonly IConfigService? _configService;

    public ActionService(IConfigService? configService = null)
    {
        _configService = configService;
    }

    public async Task ExecuteAsync(OKey key, string[] args)
    {
        switch (key.KeyType)
        {
            case OKeyType.WebPath:
                HandleWebPath(key, args);
                break;
            case OKeyType.LocalPath:
                HandleLocalPath(key, args);
                break;
            case OKeyType.JsonData:
                HandleJsonData(key);
                break;
            case OKeyType.Data:
                HandleData(key);
                break;
            case OKeyType.Rest:
                await HandleRest(key, args);
                break;
            default:
                AnsiConsole.MarkupLine("[red]Unknown key type.[/]");
                break;
        }
    }

    private void HandleWebPath(OKey key, string[] args)
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

        OpenUrl(resolved.Value);
    }

    private void HandleLocalPath(OKey key, string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = key.Value,
            UseShellExecute = true
        };
        if (args != null) foreach (var arg in args) psi.ArgumentList.Add(arg);

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

    private void HandleJsonData(OKey key)
    {
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

    private void HandleData(OKey key)
    {
        ClipboardService.SetText(key.Value);
        AnsiConsole.MarkupLine("[green]Data copied to clipboard![/]");
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
