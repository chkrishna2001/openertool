using System.CommandLine;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class ClearUrlAliasCommand : Command
{
    public ClearUrlAliasCommand(IConfigService configService, IAnsiConsole? console = null)
        : base("clear-url-alias", "Clear one global URL alias map by placeholder name.")
    {
        var _console = console ?? AnsiConsole.Console;

        var clearAliasNameArg = new Argument<string>("placeholder", "Named placeholder without angle brackets, for example env for <env>.");
        AddArgument(clearAliasNameArg);

        this.SetHandler((string placeholder) =>
        {
            var conf = configService.GetConfig();
            if (conf.GlobalUrlAliases.Remove(placeholder))
            {
                configService.SaveConfig(conf);
                _console.MarkupLine($"[green]Global URL aliases cleared for <{placeholder}>.[/]");
                return;
            }

            _console.MarkupLine($"[yellow]No global URL aliases found for <{placeholder}>.[/]");
        }, clearAliasNameArg);
    }
}
