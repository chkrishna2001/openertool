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
    public string Url { get; set; }
    public string Method { get; set; } = "GET";
    public string Body { get; set; }
    // Simple dictionary for headers could be added later
}

[JsonSerializable(typeof(RestData))]
public partial class RestDataContext : JsonSerializerContext { }

public interface IActionService
{
    Task ExecuteAsync(OKey key, string[] args);
}

public class ActionService : IActionService
{
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
        string url = key.Value;
        // Simple Placeholder replacement {0}, {1}
        try
        {
            if (args != null && args.Length > 0)
            {
                // Verify if using numeric placeholders
                if (url.Contains("{0}"))
                {
                    url = string.Format(url, args);
                }
                else
                {
                    // If no explicit format, maybe just append? Or do nothing?
                    // User requirement: "WebPath can have placeholders ... o core dev"
                    // If the user didn't put {0}, assume no replacement logic for now or custom placeholder logic?
                    // Plan said: "Supports Placeholders... {0}".
                }
            }
        }
        catch (FormatException)
        {
            AnsiConsole.MarkupLine("[yellow]Warning: Format string mismatch. Opening original URL.[/]");
        }

        AnsiConsole.MarkupLine($"[green]Opening URL:[/] [link]{url}[/]");
        OpenUrl(url);
    }

    private void HandleLocalPath(OKey key, string[] args)
    {
        string path = key.Value;
        string arguments = args.Length > 0 ? string.Join(" ", args) : string.Empty;

        AnsiConsole.MarkupLine($"[green]Starting Process:[/] {path} {arguments}");
        
        try 
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments,
                UseShellExecute = true 
            };
            Process.Start(psi);
        }
        catch(Exception ex)
        {
             AnsiConsole.MarkupLine($"[red]Error starting process:[/] {ex.Message}");
        }
    }

    private void HandleJsonData(OKey key)
    {
        // Copy JSON to clipboard and pretty print
        try 
        {
            // Validate it's valid JSON by parsing (no reflection needed)
            using var doc = JsonDocument.Parse(key.Value);
            
            // Copy to clipboard
            ClipboardService.SetText(key.Value);
            AnsiConsole.MarkupLine($"[green]JSON Data copied to clipboard![/]");
             
            // Pretty print for display using Spectre.Console.Json
            AnsiConsole.Write(new JsonText(key.Value));
        }
        catch
        {
             // Not valid JSON, just copy text
             ClipboardService.SetText(key.Value);
             AnsiConsole.MarkupLine($"[yellow]Value was not valid JSON, copied as plain text.[/]");
        }
    }

    private void HandleData(OKey key)
    {
        ClipboardService.SetText(key.Value);
        AnsiConsole.MarkupLine($"[green]Data copied to clipboard![/]");
    }

    private async Task HandleRest(OKey key, string[] args)
    {
        // Expect Value to be JSON of RestData
        try
        {
            var restData = JsonSerializer.Deserialize(key.Value, RestDataContext.Default.RestData);
            if(restData == null) 
            {
                 AnsiConsole.MarkupLine("[red]Invalid Rest Configuration.[/]");
                 return;
            }

            string url = restData.Url;
            if (args != null && args.Length > 0 && url.Contains("{0}"))
            {
                 url = string.Format(url, args);
            }

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
            
            try 
            {
                 AnsiConsole.Write(new JsonText(content));
            }
            catch
            {
                 AnsiConsole.WriteLine(content);
            }
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
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // Runtime specific hacks for opening browser
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                 AnsiConsole.MarkupLine($"[red]Could not open browser:[/] {ex.Message}");
            }
        }
    }
}
