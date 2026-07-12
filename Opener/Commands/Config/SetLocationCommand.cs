using System;
using System.CommandLine;
using System.IO;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class SetLocationCommand : Command
{
    public SetLocationCommand(IConfigService configService, IAnsiConsole? console = null)
        : base("set-location", "Set a custom encrypted data-file path.\n\n" +
                              "Example:\n" +
                              "  o config set-location \"C:\\Users\\me\\Documents\\opener.dat\"\n\n" +
                              "Notes:\n" +
                              "  - Specify the full path including filename (e.g., .dat)\n" +
                              "  - The directory must exist or be creatable\n" +
                              "  - Use a local folder; OneDrive may cause access issues\n" +
                              "  - To move existing keys, use 'export' then 'import'")
    {
        var _console = console ?? AnsiConsole.Console;

        var pathArg = new Argument<string>("path", "Full path to opener.dat, including the file name (e.g., C:\\Users\\me\\Documents\\opener.dat).");
        AddArgument(pathArg);

        this.SetHandler((string path) => 
        {
            try
            {
                // Validate path format
                if (string.IsNullOrWhiteSpace(path))
                {
                    _console.MarkupLine("[red]Path cannot be empty.[/]");
                    return;
                }

                // Check if directory exists or can be created
                var dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir))
                {
                    _console.MarkupLine("[red]Invalid path. Must include a directory component.[/]");
                    return;
                }

                // Try to create directory if it doesn't exist
                if (!Directory.Exists(dir))
                {
                    try
                    {
                        Directory.CreateDirectory(dir);
                    }
                    catch (Exception ex)
                    {
                        _console.MarkupLine($"[red]Cannot create directory:[/] {ex.Message}");
                        _console.MarkupLine("[yellow]Ensure the parent directory exists and you have write permissions.[/]");
                        return;
                    }
                }

                // Try to write a test file to validate permissions
                try
                {
                    var testFile = Path.Combine(dir, ".opener-test-" + Guid.NewGuid().ToString("N"));
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                }
                catch (Exception ex)
                {
                    _console.MarkupLine($"[red]Cannot write to directory:[/] {ex.Message}");
                    if (path.IndexOf("OneDrive", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _console.MarkupLine("[yellow]This path is on OneDrive. Try a local folder (Desktop, Documents) instead, and use 'o sync' (git-based) if you need to sync across machines.[/]");
                    }
                    else if (path.IndexOf("Documents", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _console.MarkupLine("[yellow]Documents folder may be protected or synced. Try a local folder like Desktop or C:\\Temp.[/]");
                    }
                    return;
                }

                // All checks passed, save the location
                var conf = configService.GetConfig();
                conf.StorageLocation = path;
                configService.SaveConfig(conf);
                _console.MarkupLine($"[green]Storage location updated to:[/] {path}");
                _console.MarkupLine("[yellow]Note: Existing keys were not moved. Use 'export' then 'import' if needed.[/]");
            }
            catch (Exception ex)
            {
                _console.MarkupLine($"[red]Error updating storage location:[/] {ex.Message}");
            }
        }, pathArg);
    }
}
