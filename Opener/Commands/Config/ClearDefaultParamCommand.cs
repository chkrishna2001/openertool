using System.CommandLine;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class ClearDefaultParamCommand : Command
{
    public ClearDefaultParamCommand(IConfigService configService, IAnsiConsole? console = null)
        : base("clear-default-param", "Clear one global default value by placeholder name.")
    {
        var _console = console ?? AnsiConsole.Console;

        var clearDefaultNameArg = new Argument<string>("placeholder", "Named placeholder without angle brackets, for example user for <user>.");
        AddArgument(clearDefaultNameArg);

        this.SetHandler((string placeholder) =>
        {
            var conf = configService.GetConfig();
            if (conf.GlobalDefaultParams.Remove(placeholder))
            {
                configService.SaveConfig(conf);
                _console.MarkupLine($"[green]Global default param cleared for <{placeholder}>.[/]");
                return;
            }

            _console.MarkupLine($"[yellow]No global default param found for <{placeholder}>.[/]");
        }, clearDefaultNameArg);
    }
}
