using System;
using System.CommandLine;
using System.Linq;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands;

public class ViewCommand : Command
{
    public ViewCommand(IStorageService storageService, IAnsiConsole? console = null)
        : base("view", "View the raw details and stored value of a key.")
    {
        var _console = console ?? AnsiConsole.Console;

        var keyArg = new Argument<string>("key", "The name of the key to view.");
        AddArgument(keyArg);

        this.SetHandler((string keyName) =>
        {
            var keys = storageService.GetKeys();
            var key = keys.FirstOrDefault(x => x?.Key != null && x.Key.Equals(keyName, StringComparison.OrdinalIgnoreCase));
            if (key == null)
            {
                _console.MarkupLine($"[red]Key '{keyName}' not found.[/]");
                return;
            }

            CommandHelpers.DisplayKey(key, _console);
        }, keyArg);
    }
}
