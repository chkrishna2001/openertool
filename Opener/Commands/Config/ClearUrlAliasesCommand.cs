using System.CommandLine;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class ClearUrlAliasesCommand : Command
{
    public ClearUrlAliasesCommand(IConfigService configService, IAnsiConsole? console = null)
        : base("clear-url-aliases", "Clear all global URL alias maps. Per-key aliases are not changed.")
    {
        var _console = console ?? AnsiConsole.Console;

        this.SetHandler(() =>
        {
            if (!_console.Confirm("⚠️  Clear all global URL aliases? This cannot be undone.", false))
            {
                _console.MarkupLine("[yellow]Cancelled.[/]");
                return;
            }
            var conf = configService.GetConfig();
            conf.GlobalUrlAliases.Clear();
            configService.SaveConfig(conf);
            _console.MarkupLine("[green]Global URL aliases cleared.[/]");
        });
    }
}
