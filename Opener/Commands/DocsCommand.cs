using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands;

public class DocsCommand : Command
{
    public DocsCommand(IConfigService configService, IAnsiConsole? console = null)
        : base("docs", "Generate and open the interactive HTML documentation in your default browser.")
    {
        var _console = console ?? AnsiConsole.Console;

        var outputOpt = new Option<string?>(new[] { "-o", "--output" }, "Write documentation HTML to a specific file path and exit without opening browser.");
        AddOption(outputOpt);

        this.SetHandler((string? outputPath) =>
        {
            try
            {
                var assembly = typeof(DocsCommand).Assembly;
                var versionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                var versionStr = versionAttribute?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "1.0.0";
                if (versionStr.Contains('+'))
                {
                    versionStr = versionStr.Split('+')[0];
                }

                var htmlContent = DocsHtmlGenerator.GetHtml(versionStr);

                if (!string.IsNullOrEmpty(outputPath))
                {
                    var fullPath = Path.GetFullPath(outputPath);
                    var dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.WriteAllText(fullPath, htmlContent);
                    _console.MarkupLine($"[green]Documentation successfully saved to: {fullPath}[/]");
                    return;
                }

                var contextRoot = ExecutionContextHelper.GetExecutionContextPath();
                var configDir = Path.Combine(contextRoot, ".opener");
                
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                var htmlPath = Path.Combine(configDir, "docs.html");
                File.WriteAllText(htmlPath, htmlContent);

                _console.MarkupLine($"[yellow]Generating documentation at:[/] {htmlPath}");
                _console.MarkupLine("[green]Opening documentation in your default browser...[/]");

                OpenUrl(htmlPath);
            }
            catch (Exception ex)
            {
                _console.MarkupLine($"[red]Error generating or opening documentation:[/] {ex.Message}");
            }
        }, outputOpt);
    }

    private static void OpenUrl(string url)
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
}
