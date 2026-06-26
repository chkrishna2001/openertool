using System;
using System.CommandLine;
using System.IO;
using System.Text.Json;
using Opener.Models;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class SetDefaultParamsCommand : Command
{
    public SetDefaultParamsCommand(IConfigService configService, IAnsiConsole? console = null)
        : base("set-default-params", "Set one global default value, or replace all global defaults from a JSON file.\n\n" +
                                     "Defaults are used when a named placeholder is not supplied by positional args or key=value args.\n\n" +
                                     "Examples:\n" +
                                     "  o config set-default-params user kchirravuri\n" +
                                     "  o config set-default-params region us\n" +
                                     "  o config set-default-params --file defaults.json")
    {
        var _console = console ?? AnsiConsole.Console;

        var defaultPlaceholderArg = new Argument<string?>("placeholder", "Named placeholder without angle brackets, for example user for <user>. Omit when using --file.") { Arity = ArgumentArity.ZeroOrOne };
        var defaultValueArg = new Argument<string?>("value", "Default value to use when the placeholder is omitted. Omit when using --file.") { Arity = ArgumentArity.ZeroOrOne };
        var defaultParamsFileOpt = new Option<string?>(new[] { "--file" }, "Path to a JSON file containing all global default params.");
        
        AddArgument(defaultPlaceholderArg);
        AddArgument(defaultValueArg);
        AddOption(defaultParamsFileOpt);

        this.SetHandler((string? placeholder, string? value, string? file) =>
        {
            var conf = configService.GetConfig();

            if (!string.IsNullOrWhiteSpace(file))
            {
                try
                {
                    conf.GlobalDefaultParams = JsonSerializer.Deserialize(CommandHelpers.ReadJsonFile(file), OpenerJsonContext.Default.DictionaryStringString)
                        ?? new(StringComparer.OrdinalIgnoreCase);
                    configService.SaveConfig(conf);
                    _console.MarkupLine("[green]Global default params updated.[/]");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    _console.MarkupLine($"[red]Unable to read default params JSON file:[/] {ex.Message}");
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(placeholder) || value == null)
            {
                _console.MarkupLine("[red]Missing placeholder or value.[/] Use 'o config set-default-params user kchirravuri' or 'o config set-default-params --file defaults.json'.");
                return;
            }

            conf.GlobalDefaultParams[placeholder] = value;
            configService.SaveConfig(conf);
            _console.MarkupLine($"[green]Global default param updated for <{placeholder}>.[/]");
        }, defaultPlaceholderArg, defaultValueArg, defaultParamsFileOpt);
    }
}
