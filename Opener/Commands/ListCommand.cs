using System;
using System.CommandLine;
using System.Linq;
using Opener.Models;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands;

public class ListCommand : Command
{
    public ListCommand(IStorageService storageService, IAnsiConsole? console = null)
        : base("list", "List all stored keys with type and a safe value preview.")
    {
        var _console = console ?? AnsiConsole.Console;

        var listSearchOpt = new Option<string?>(new[] { "-s", "--search" }, "Filter keys by case-insensitive substring across key and description.") { Arity = ArgumentArity.ZeroOrOne };
        AddOption(listSearchOpt);

        this.SetHandler((string? search) =>
        {
            var keys = storageService.GetKeys();
            if (keys == null || keys.Count == 0) 
            { 
                _console.MarkupLine("[yellow]No keys found.[/]"); 
                return; 
            }
            var filtered = keys.Where(x => x != null && !x.Key.StartsWith("__")).ToList();
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(k => (!string.IsNullOrWhiteSpace(k.Key) && k.Key.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                                             (!string.IsNullOrWhiteSpace(k.Description) && k.Description.Contains(search, StringComparison.OrdinalIgnoreCase))).OrderBy(x => x.Key).ToList();
            }
            if (filtered.Count == 0) 
            { 
                _console.MarkupLine("[yellow]No keys match the search term.[/]"); 
                return; 
            }
            var table = new Table();
            table.AddColumn("Key");
            table.AddColumn("Type");
            table.AddColumn("Elevated");
            table.AddColumn("Value (Preview)");
            foreach (var key in filtered.OrderBy(x => x.Key))
            {
                string valPreview = (key.Value ?? string.Empty).Length > 50 ? key.Value!.Substring(0, 47) + "..." : (key.Value ?? string.Empty);
                if (key.KeyType == OKeyType.Data || key.KeyType == OKeyType.JsonData) valPreview = "********";
                table.AddRow(key.Key ?? "N/A", key.KeyType.ToString(), key.Elevated ? "[red]Yes[/]" : "No", Markup.Escape(valPreview));
            }
            _console.Write(table);
        }, listSearchOpt);
    }
}
