using System.CommandLine;
using System.Threading.Tasks;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands;

public class SyncCommand : Command
{
    public SyncCommand(IConfigService configService, IGitSyncService gitSyncService, IAnsiConsole? console = null)
        : base("sync", "Push/pull the encrypted vault through a git remote instead of a cloud-storage client.\n\n" +
                      "Setup:\n" +
                      "  o config set-sync-remote git@github.com:me/opener-vault.git\n" +
                      "  o config set-sync-token <token>   # only needed for https:// remotes\n\n" +
                      "Examples:\n" +
                      "  o sync push\n" +
                      "  o sync pull\n" +
                      "  o sync status")
    {
        var _console = console ?? AnsiConsole.Console;

        var pushCommand = new Command("push", "Push the current vault to the sync remote.");
        pushCommand.SetHandler(async () =>
        {
            var result = await gitSyncService.PushAsync();
            _console.MarkupLine(result.Success ? $"[green]{Markup.Escape(result.Message)}[/]" : $"[red]{Markup.Escape(result.Message)}[/]");
        });

        var pullCommand = new Command("pull", "Pull the vault from the sync remote (backs up your current vault first).");
        pullCommand.SetHandler(async () =>
        {
            var result = await gitSyncService.PullAsync();
            _console.MarkupLine(result.Success ? $"[green]{Markup.Escape(result.Message)}[/]" : $"[red]{Markup.Escape(result.Message)}[/]");
        });

        var statusCommand = new Command("status", "Show whether sync is configured.");
        statusCommand.SetHandler(() =>
        {
            var conf = configService.GetConfig();
            if (string.IsNullOrWhiteSpace(conf.GitSyncRemote))
            {
                _console.MarkupLine("[yellow]No sync remote configured.[/] Run 'o config set-sync-remote <url>' to set one up.");
                return;
            }
            _console.MarkupLine($"Remote: {Markup.Escape(conf.GitSyncRemote)}");
            _console.MarkupLine($"Auto-sync: {(conf.AutoSyncEnabled ? "[green]enabled[/]" : "disabled")}");
            _console.MarkupLine(gitSyncService.IsConfigured() ? "[green]Ready to sync.[/]" : "[yellow]git was not found on PATH.[/]");
        });

        AddCommand(pushCommand);
        AddCommand(pullCommand);
        AddCommand(statusCommand);
    }
}
