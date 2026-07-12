using System.CommandLine;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class SetSyncTokenCommand : Command
{
    public SetSyncTokenCommand(IAnsiConsole? console = null)
        : base("set-sync-token", "Store a personal access token for an HTTPS git sync remote.\n\n" +
                                "Only needed for https:// remotes - SSH remotes use your existing SSH agent/keys instead.\n" +
                                "Stored in your OS keychain (or the encrypted fallback store), in a slot separate\n" +
                                "from your vault's own unlock password.\n\n" +
                                "Example:\n" +
                                "  o config set-sync-token ghp_xxxxxxxxxxxx")
    {
        var _console = console ?? AnsiConsole.Console;

        var tokenArg = new Argument<string>("token", "Personal access token with push access to the sync remote.");
        AddArgument(tokenArg);

        this.SetHandler((string token) =>
        {
            var credentialService = CredentialServiceFactory.Create("git-sync");
            credentialService.SetPassword(token);
            _console.MarkupLine("[green]Sync token stored.[/]");
        }, tokenArg);
    }
}
