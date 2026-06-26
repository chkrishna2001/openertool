using System.CommandLine;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class ClearDefaultParamsCommand : Command
{
    public ClearDefaultParamsCommand(IConfigService configService, IAnsiConsole? console = null)
        : base("clear-default-params", "Clear all global default placeholder values. Per-key defaults are not changed.")
    {
        var _console = console ?? AnsiConsole.Console;

        this.SetHandler(() =>
        {
            var conf = configService.GetConfig();
            conf.GlobalDefaultParams.Clear();
            configService.SaveConfig(conf);
            _console.MarkupLine("[green]Global default params cleared.[/]");
        });
    }
}
