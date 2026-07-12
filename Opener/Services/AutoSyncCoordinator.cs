using System;
using System.Threading.Tasks;
using Spectre.Console;

namespace Opener.Services;

/// <summary>
/// Fires an opt-in background push after a mutating command succeeds, and gives it a
/// bounded grace period to finish before the (otherwise short-lived) process exits.
/// Failures are surfaced as a non-fatal warning only - they never fail the command that
/// triggered them.
/// </summary>
public static class AutoSyncCoordinator
{
    private static Task? _pending;

    public static void TriggerIfEnabled(IConfigService? configService, IGitSyncService? gitSyncService)
    {
        if (configService == null || gitSyncService == null)
        {
            return;
        }

        if (!configService.GetConfig().AutoSyncEnabled)
        {
            return;
        }

        _pending = Task.Run(async () =>
        {
            try
            {
                var result = await gitSyncService.PushAsync();
                if (!result.Success)
                {
                    AnsiConsole.MarkupLine($"[dim yellow]Auto-sync:[/] [dim]{Markup.Escape(result.Message)}[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[dim yellow]Auto-sync failed:[/] [dim]{Markup.Escape(ex.Message)}[/]");
            }
        });
    }

    /// <summary>
    /// Gives any in-flight auto-sync a bounded chance to finish. If it's still running
    /// past the timeout, the process exits anyway - the next mutation will retry.
    /// </summary>
    public static async Task WaitForPendingAsync(TimeSpan timeout)
    {
        var pending = _pending;
        if (pending == null)
        {
            return;
        }

        await Task.WhenAny(pending, Task.Delay(timeout));
    }
}
