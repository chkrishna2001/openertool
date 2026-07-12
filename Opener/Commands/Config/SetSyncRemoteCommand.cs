using System.CommandLine;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class SetSyncRemoteCommand : Command
{
    public SetSyncRemoteCommand(IConfigService configService, IAnsiConsole? console = null)
        : base("set-sync-remote", "Set the git remote used by 'o sync' to push/pull the encrypted vault.\n\n" +
                                 "Examples:\n" +
                                 "  o config set-sync-remote git@github.com:me/opener-vault.git\n" +
                                 "  o config set-sync-remote https://github.com/me/opener-vault.git\n\n" +
                                 "Notes:\n" +
                                 "  - SSH remotes use your existing SSH agent/keys - no credentials stored by Opener.\n" +
                                 "  - HTTPS remotes need a token: 'o config set-sync-token <token>'.")
    {
        var _console = console ?? AnsiConsole.Console;

        var remoteArg = new Argument<string>("remote", "Git remote URL, e.g. git@github.com:me/opener-vault.git");
        AddArgument(remoteArg);

        this.SetHandler((string remote) =>
        {
            var conf = configService.GetConfig();
            conf.GitSyncRemote = remote;
            configService.SaveConfig(conf);
            _console.MarkupLine($"[green]Sync remote set to:[/] {remote}");
        }, remoteArg);
    }
}
