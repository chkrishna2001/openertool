using System.CommandLine;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class EnableAutoSyncCommand : Command
{
    public EnableAutoSyncCommand(IConfigService configService, IAnsiConsole? console = null)
        : base("enable-auto-sync", "Automatically push to the sync remote after 'add', 'update', 'delete', and 'import'.\n\n" +
                                  "Requires a sync remote: 'o config set-sync-remote <url>' first.\n" +
                                  "Sync runs in the background and never fails the command that triggered it.")
    {
        var _console = console ?? AnsiConsole.Console;

        this.SetHandler(() =>
        {
            var conf = configService.GetConfig();
            if (string.IsNullOrWhiteSpace(conf.GitSyncRemote))
            {
                _console.MarkupLine("[yellow]Warning:[/] No sync remote configured yet. Set one with 'o config set-sync-remote <url>' before this takes effect.");
            }
            conf.AutoSyncEnabled = true;
            configService.SaveConfig(conf);
            _console.MarkupLine("[green]Auto-sync enabled.[/]");
        });
    }
}
