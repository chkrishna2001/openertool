using System;
using System.CommandLine;
using System.Linq;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands;

public class DeleteCommand : Command
{
    public DeleteCommand(IStorageService storageService, IAnsiConsole? console = null, IConfigService? configService = null, IGitSyncService? gitSyncService = null)
        : base("delete", "Delete a stored key.\n\n" +
                        "Example:\n" +
                        "  o delete jira")
    {
        var _console = console ?? AnsiConsole.Console;

        var keyArg = new Argument<string>("key", "Existing key name to delete.");
        var confirmOpt = new Option<bool>(new[] { "-y", "--yes" }, "Skip confirmation prompt.");

        AddArgument(keyArg);
        AddOption(confirmOpt);

        this.SetHandler((string k, bool skipConfirm) =>
        {
            var keys = storageService.GetKeys();
            var existing = keys.FirstOrDefault(x => x?.Key != null && x.Key.Equals(k, StringComparison.OrdinalIgnoreCase));
            if (existing == null) 
            { 
                _console.MarkupLine($"[red]Key '{k}' not found.[/]"); 
                return; 
            }
            
            if (!skipConfirm && !_console.Confirm($"⚠️  Delete key '{k}'? This cannot be undone.", false))
            {
                _console.MarkupLine("[yellow]Cancelled.[/]");
                return;
            }
            
            keys.Remove(existing);
            storageService.SaveKeys(keys);
            _console.MarkupLine($"[green]Key '{k}' deleted.[/]");
            AutoSyncCoordinator.TriggerIfEnabled(configService, gitSyncService);
        }, keyArg, confirmOpt);
    }
}
