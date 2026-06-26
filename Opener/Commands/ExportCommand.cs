using System;
using System.CommandLine;
using System.IO;
using System.Text.Json;
using Opener.Models;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands;

public class ExportCommand : Command
{
    public ExportCommand(IStorageService storageService, IAnsiConsole? console = null)
        : base("export", "Export all keys to a portable encrypted backup file.\n\n" +
                        "Example:\n" +
                        "  o export backup.dat --password my-export-password")
    {
        var _console = console ?? AnsiConsole.Console;

        var exportPathArg = new Argument<string>("file", "Output encrypted backup file path.");
        var exportPassOpt = new Option<string>(new[] { "-p", "--password" }, "Password for the export file. Omit it to be prompted interactively.");
        
        AddArgument(exportPathArg);
        AddOption(exportPassOpt);

        this.SetHandler((string path, string? passInput) => 
        {
            var keys = storageService.GetKeys();
            if (keys.Count == 0)
            {
                _console.MarkupLine("[yellow]No keys to export.[/]");
                return;
            }

            string pass = passInput ?? string.Empty;
            if (string.IsNullOrEmpty(pass))
            {
                pass = _console.Prompt(new TextPrompt<string>("Enter password for export file:").Secret());
                var confirm = _console.Prompt(new TextPrompt<string>("Confirm password:").Secret());
                if (pass != confirm) 
                {
                    _console.MarkupLine("[red]Passwords do not match.[/]");
                    return;
                }
            }

            var tempEncryptor = new PortableEncryptionService(pass);
            try 
            {
                var json = JsonSerializer.Serialize(keys, OpenerJsonContext.Default.ListOKey);
                var encrypted = tempEncryptor.Encrypt(json);
                File.WriteAllText(path, encrypted);
                _console.MarkupLine($"[green]Exported {keys.Count} keys to {path}[/]");
            } 
            catch (Exception ex) 
            {
                _console.MarkupLine($"[red]Export failed:[/] {ex.Message}");
            }
        }, exportPathArg, exportPassOpt);
    }
}
