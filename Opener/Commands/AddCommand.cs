using System;
using System.CommandLine;
using System.Linq;
using System.Text.Json;
using Opener.Models;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands;

public class AddCommand : Command
{
    public AddCommand(IStorageService storageService, IAnsiConsole? console = null, IConfigService? configService = null, IGitSyncService? gitSyncService = null)
        : base("add", "Add a new key.\n\n" +
                     "Examples:\n" +
                     "  o add token \"secret-value\" -t Data\n" +
                     "  o add jira \"https://jira.company.com/browse/{0}\" -t WebPath\n" +
                     "  o add api \"https://nvidia<env>.domain.com/<region>/<user>\" -t WebPath\n" +
                     "  o add github JBSWY3DPEHPK3PXP -t Totp   # base32 secret, or paste a full otpauth:// URI\n" +
                     "  o config set-url-aliases env d=-dev u=-uat p=\n" +
                     "  o config set-default-params user kchirravuri")
    {
        var _console = console ?? AnsiConsole.Console;

        var keyArg = new Argument<string>("key", "Unique key name, for example jira or api.");
        var valArg = new Argument<string>("value", "Stored value. Meaning depends on --type: URL template, local path, data, JSON, REST JSON, or a TOTP base32 secret / otpauth:// URI.");
        var typeOpt = new Option<OKeyType>(new[] { "-t", "--type" }, () => OKeyType.Data, "Key type: WebPath, LocalPath, Data, JsonData, Rest, EmailTemplate, CalendarEvent, or Totp.");
        var urlAliasesOpt = new Option<string?>(new[] { "--url-aliases" }, "Per-key alias map JSON. " + CommandHelpers.UrlAliasJsonHelp);
        var defaultParamsOpt = new Option<string?>(new[] { "--default-params" }, "Per-key default params JSON. " + CommandHelpers.DefaultParamsJsonHelp);
        var elevatedOpt = new Option<bool>(new[] { "-e", "--elevated" }, "Execute the key in elevated mode (admin/sudo)");

        AddArgument(keyArg);
        AddArgument(valArg);
        AddOption(typeOpt);
        AddOption(urlAliasesOpt);
        AddOption(defaultParamsOpt);
        AddOption(elevatedOpt);

        this.SetHandler((string k, string v, OKeyType t, string? urlAliasesJson, string? defaultParamsJson, bool elevated) =>
        {
            var keys = storageService.GetKeys();
            if (keys.Any(x => x?.Key != null && x.Key.Equals(k, StringComparison.OrdinalIgnoreCase)))
            {
                _console.MarkupLine($"[red]Key '{k}' already exists. Use update command.[/]");
                return;
            }

            string resolvedValue = v ?? string.Empty;
            if (t == OKeyType.JsonData || t == OKeyType.Rest || t == OKeyType.EmailTemplate || t == OKeyType.CalendarEvent)
            {
                resolvedValue = CommandHelpers.ResolveJsonInput(v);
            }
            else if (t == OKeyType.Totp)
            {
                resolvedValue = TotpService.ExtractSecret(v ?? string.Empty);
                try
                {
                    TotpService.GenerateCode(resolvedValue);
                }
                catch (Exception ex)
                {
                    _console.MarkupLine($"[red]Invalid TOTP secret:[/] {ex.Message}");
                    return;
                }
            }

            var newKey = new OKey { Key = k ?? string.Empty, Value = resolvedValue, KeyType = t, Elevated = elevated };

            var resolvedUrlAliasesJson = CommandHelpers.ResolveJsonInput(urlAliasesJson);
            if (!string.IsNullOrWhiteSpace(resolvedUrlAliasesJson))
            {
                try 
                { 
                    newKey.UrlAliases = JsonSerializer.Deserialize(resolvedUrlAliasesJson, OpenerJsonContext.Default.DictionaryStringDictionaryStringString) ?? newKey.UrlAliases; 
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
                    newKey.DefaultParams = JsonSerializer.Deserialize(resolvedDefaultParamsJson, OpenerJsonContext.Default.DictionaryStringString) ?? newKey.DefaultParams; 
                }
                catch 
                { 
                    _console.MarkupLine("[yellow]Warning:[/] Invalid JSON for --default-params, ignoring."); 
                }
            }

            keys.Add(newKey);
            storageService.SaveKeys(keys);
            _console.MarkupLine($"[green]Key '{k}' added successfully![/]");
            AutoSyncCoordinator.TriggerIfEnabled(configService, gitSyncService);
        }, keyArg, valArg, typeOpt, urlAliasesOpt, defaultParamsOpt, elevatedOpt);
    }
}
