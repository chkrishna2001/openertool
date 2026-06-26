using System;
using System.CommandLine;
using System.Linq;
using System.Text.Json;
using Opener.Models;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands;

public class UpdateCommand : Command
{
    public UpdateCommand(IStorageService storageService, IAnsiConsole? console = null)
        : base("update", "Update an existing key's value and optionally replace its per-key URL aliases/default params.\n\n" +
                        "Examples:\n" +
                        "  o update jira \"https://jira.company.com/browse/{0}\"\n" +
                        "  o update api \"https://nvidia<env>.domain.com/<region>/<user>\"\n" +
                        "  o config set-url-aliases env d=-dev u=-uat p=")
    {
        var _console = console ?? AnsiConsole.Console;

        var keyArg = new Argument<string>("key", "Existing key name to update.");
        var valArg = new Argument<string>("value", "Replacement stored value.");
        var urlAliasesOpt = new Option<string?>(new[] { "--url-aliases" }, "Replace per-key alias map JSON. " + CommandHelpers.UrlAliasJsonHelp);
        var defaultParamsOpt = new Option<string?>(new[] { "--default-params" }, "Replace per-key default params JSON. " + CommandHelpers.DefaultParamsJsonHelp);
        var elevatedOpt = new Option<bool?>(new[] { "-e", "--elevated" }, "Update elevated execution mode (true/false)");

        AddArgument(keyArg);
        AddArgument(valArg);
        AddOption(urlAliasesOpt);
        AddOption(defaultParamsOpt);
        AddOption(elevatedOpt);

        this.SetHandler((string k, string v, string? urlAliasesJson, string? defaultParamsJson, bool? elevated) =>
        {
            var keys = storageService.GetKeys();
            var existing = keys.FirstOrDefault(x => x?.Key != null && x.Key.Equals(k, StringComparison.OrdinalIgnoreCase));
            if (existing == null) 
            { 
                _console.MarkupLine($"[red]Key '{k}' not found.[/]"); 
                return; 
            }
            existing.Value = v ?? string.Empty;
            
            var resolvedUrlAliasesJson = CommandHelpers.ResolveJsonInput(urlAliasesJson);
            if (!string.IsNullOrWhiteSpace(resolvedUrlAliasesJson))
            {
                try 
                { 
                    existing.UrlAliases = JsonSerializer.Deserialize(resolvedUrlAliasesJson, OpenerJsonContext.Default.DictionaryStringDictionaryStringString) ?? existing.UrlAliases; 
                }
                catch 
                { 
                    _console.MarkupLine("[yellow]Warning:[/] Invalid JSON for --url-aliases, ignoring."); 
                }
            }

            var resolvedDefaultParamsJson = CommandHelpers.ResolveJsonInput(defaultParamsJson);
            if (!string.IsNullOrWhiteSpace(resolvedDefaultParamsJson))
            {
                try 
                { 
                    existing.DefaultParams = JsonSerializer.Deserialize(resolvedDefaultParamsJson, OpenerJsonContext.Default.DictionaryStringString) ?? existing.DefaultParams; 
                }
                catch 
                { 
                    _console.MarkupLine("[yellow]Warning:[/] Invalid JSON for --default-params, ignoring."); 
                }
            }

            if (elevated.HasValue)
            {
                existing.Elevated = elevated.Value;
            }

            storageService.SaveKeys(keys);
            _console.MarkupLine($"[green]Key '{k}' updated successfully![/]");
        }, keyArg, valArg, urlAliasesOpt, defaultParamsOpt, elevatedOpt);
    }
}
