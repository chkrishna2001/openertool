using System.Collections.Generic;
using System.Linq;
using Opener.Models;
using Spectre.Console;

namespace Opener.Commands;

public static class InteractiveKeyPicker
{
    private static readonly OKey CancelSentinel = new() { Key = "__cancel__" };

    /// <summary>
    /// Shows a searchable selection prompt over the given keys. Returns null if the user
    /// cancels, if the console isn't interactive (falls back to a static table instead of
    /// hanging), or if there are no candidates.
    /// </summary>
    public static OKey? Pick(IAnsiConsole console, List<OKey> candidates, string title)
    {
        if (candidates.Count == 0)
        {
            console.MarkupLine("[yellow]No keys found.[/]");
            return null;
        }

        if (!console.Profile.Capabilities.Interactive)
        {
            RenderStaticTable(console, candidates);
            return null;
        }

        var ordered = candidates.OrderBy(k => k.Key).ToList();
        ordered.Add(CancelSentinel);

        var prompt = new SelectionPrompt<OKey>()
            .Title(title)
            .PageSize(15)
            .MoreChoicesText("[grey](Move up/down to reveal more, type to search)[/]")
            .EnableSearch()
            .UseConverter(FormatChoice)
            .AddChoices(ordered);

        var selected = console.Prompt(prompt);
        return ReferenceEquals(selected, CancelSentinel) ? null : selected;
    }

    private static string FormatChoice(OKey key)
    {
        if (ReferenceEquals(key, CancelSentinel))
        {
            return "[grey]Cancel[/]";
        }

        var description = string.IsNullOrWhiteSpace(key.Description) ? string.Empty : $" - {key.Description}";
        return Markup.Escape($"{key.Key}  [{key.KeyType}]{description}");
    }

    private static void RenderStaticTable(IAnsiConsole console, List<OKey> candidates)
    {
        var table = new Table();
        table.AddColumn("Key");
        table.AddColumn("Type");
        table.AddColumn("Description");
        foreach (var key in candidates.OrderBy(k => k.Key))
        {
            table.AddRow(Markup.Escape(key.Key ?? "N/A"), key.KeyType.ToString(), Markup.Escape(key.Description ?? string.Empty));
        }
        console.Write(table);
    }
}
