using System.CommandLine;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class ClearPasswordCommand : Command
{
    public ClearPasswordCommand(ICredentialService credentialService, IAnsiConsole? console = null)
        : base("clear-password", "Clear the cached portable-mode password from the local credential store.")
    {
        var _console = console ?? AnsiConsole.Console;

        this.SetHandler(() => 
        {
            credentialService.ClearPassword();
            _console.MarkupLine("[green]Cached password cleared.[/]");
        });
    }
}
