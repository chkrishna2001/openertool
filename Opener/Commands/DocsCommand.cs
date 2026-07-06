using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
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

        this.SetHandler(() =>
        {
            try
            {
                var contextRoot = ExecutionContextHelper.GetExecutionContextPath();
                var configDir = Path.Combine(contextRoot, ".opener");
                
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }

                var htmlPath = Path.Combine(configDir, "docs.html");
                var htmlContent = DocsHtmlGenerator.GetHtml();

                File.WriteAllText(htmlPath, htmlContent);

                _console.MarkupLine($"[yellow]Generating documentation at:[/] {htmlPath}");
                _console.MarkupLine("[green]Opening documentation in your default browser...[/]");

                OpenUrl(htmlPath);
            }
            catch (Exception ex)
            {
                _console.MarkupLine($"[red]Error generating or opening documentation:[/] {ex.Message}");
            }
        });
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
