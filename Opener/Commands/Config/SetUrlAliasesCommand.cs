using System;
using System.CommandLine;
using System.IO;
using System.Text.Json;
using Opener.Models;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class SetUrlAliasesCommand : Command
{
    public SetUrlAliasesCommand(IConfigService configService, IAnsiConsole? console = null)
        : base("set-url-aliases", "Set a global URL alias map for one placeholder, or replace all global alias maps from a JSON file.\n\n" +
                                  "Aliases translate compact input values before a URL template is opened. For a template containing <env>, this can turn 'd' into '-dev' or 'p' into an empty production suffix.\n\n" +
                                  "Examples:\n" +
                                  "  o config set-url-aliases env d=-dev u=-uat p=\n" +
                                  "  o config set-url-aliases region us=na eu=emea\n" +
                                  "  o config set-url-aliases --file aliases.json")
    {
        var _console = console ?? AnsiConsole.Console;

        var urlAliasesPlaceholderArg = new Argument<string?>("placeholder", "Named placeholder without angle brackets, for example env for <env>. Omit when using --file.") { Arity = ArgumentArity.ZeroOrOne };
        var urlAliasesPairsArg = new Argument<string[]>("aliases", "Alias pairs in input=replacement form. Empty replacements are allowed, for example p=.") { Arity = ArgumentArity.ZeroOrMore };
        var urlAliasesFileOpt = new Option<string?>(new[] { "--file" }, "Path to a JSON file containing all global URL alias maps.");
        
        AddArgument(urlAliasesPlaceholderArg);
        AddArgument(urlAliasesPairsArg);
        AddOption(urlAliasesFileOpt);

        this.SetHandler((string? placeholder, string[] pairs, string? file) =>
        {
            var conf = configService.GetConfig();

            if (!string.IsNullOrWhiteSpace(file))
            {
                try
                {
                    conf.GlobalUrlAliases = JsonSerializer.Deserialize(CommandHelpers.ReadJsonFile(file), OpenerJsonContext.Default.DictionaryStringDictionaryStringString)
                        ?? new(StringComparer.OrdinalIgnoreCase);
                    configService.SaveConfig(conf);
                    _console.MarkupLine("[green]Global URL aliases updated.[/]");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    _console.MarkupLine($"[red]Unable to read alias JSON file:[/] {ex.Message}");
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(placeholder))
            {
                _console.MarkupLine("[red]Missing placeholder.[/] Use 'o config set-url-aliases env d=-dev' or 'o config set-url-aliases --file aliases.json'.");
                return;
            }

            if (pairs.Length == 0)
            {
                _console.MarkupLine("[red]Missing alias pairs.[/] Use input=replacement pairs, for example d=-dev u=-uat p=.");
                return;
            }

            if (!CommandHelpers.TryParsePairs(pairs, out var aliases, out var error))
            {
                _console.MarkupLine($"[red]Invalid alias pair:[/] {error}");
                return;
            }

            conf.GlobalUrlAliases[placeholder] = aliases;
            configService.SaveConfig(conf);
            _console.MarkupLine($"[green]Global URL aliases updated for <{placeholder}>.[/]");
        }, urlAliasesPlaceholderArg, urlAliasesPairsArg, urlAliasesFileOpt);
    }
}
