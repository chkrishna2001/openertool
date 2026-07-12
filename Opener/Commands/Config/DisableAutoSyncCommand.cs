using System.CommandLine;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class DisableAutoSyncCommand : Command
{
    public DisableAutoSyncCommand(IConfigService configService, IAnsiConsole? console = null)
        : base("disable-auto-sync", "Stop automatically pushing to the sync remote after mutating commands.")
    {
        var _console = console ?? AnsiConsole.Console;

        this.SetHandler(() =>
        {
            var conf = configService.GetConfig();
            conf.AutoSyncEnabled = false;
            configService.SaveConfig(conf);
            _console.MarkupLine("[green]Auto-sync disabled.[/]");
        });
    }
}
