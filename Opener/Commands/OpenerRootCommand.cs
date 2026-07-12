using System;
using System.CommandLine;
using System.Linq;
using System.Threading.Tasks;
using Opener.Models;
using Opener.Services;
using Opener.Commands.Config;
using Spectre.Console;

namespace Opener.Commands;

public class OpenerRootCommand : RootCommand
{
    public OpenerRootCommand(
        IConfigService configService,
        IStorageService storageService,
        IActionService actionService,
        ICredentialService credentialService,
        IGraphAuthService graphAuthService,
        IAnsiConsole? console = null)
        : base("Opener Tool - quickly open links, paths, stored data, and REST shortcuts.\n\n" +
               "Examples:\n" +
               "  o list\n" +
               "  o list -s github\n" +
               "  o add jira \"https://jira.company.com/browse/{0}\" -t WebPath\n" +
               "  o jira PROJ-123\n" +
               "  o jira -s    # search for keys containing 'jira' and execute if single match\n" +
               "  o githubtoken -r  # print token to stdout instead of copying\n" +
               "  o githubtoken -c  # force copy token to clipboard instead of opening\n" +
               "  o add api \"https://nvidia<env>.domain.com/<region>/<user>\" -t WebPath --url-aliases '{ \"env\": { \"d\": \"-dev\", \"p\": \"\" } }' --default-params '{ \"user\": \"kchirravuri\" }'\n" +
               "  o api env=d region=us")
    {
        var _console = console ?? AnsiConsole.Console;

        // --- Arguments for Implicit Key ---
        var keyArgument = new Argument<string>("key", "Stored key to execute. Use 'o list' to see available keys.") { Arity = ArgumentArity.ZeroOrOne, IsHidden = true };
        var actArgsArgument = new Argument<string[]>("args", "Arguments passed to the key action. URL templates accept positional values or named values like env=d region=us.") { Arity = ArgumentArity.ZeroOrMore, IsHidden = true };
        AddArgument(keyArgument);
        AddArgument(actArgsArgument);
        
        // Global options for implicit execution
        var returnOpt = new Option<bool>(new[] { "-r", "--return" }, "Return the resolved value to stdout instead of copying/opening");
        var copyOpt = new Option<bool>(new[] { "-c", "--copy" }, "Force copy the resolved value to clipboard instead of performing the default action");
        var searchFlagOpt = new Option<bool>(new[] { "-s", "--search" }, "Treat the provided key as a search term and lookup by substring (key + description)");
        var elevatedOpt = new Option<bool>(new[] { "-e", "--elevated" }, "Execute the command/script in elevated mode (admin/sudo)");
        var viewOpt = new Option<bool>(new[] { "-v", "--view" }, "View the raw details and stored value of the key");
        AddOption(returnOpt);
        AddOption(copyOpt);
        AddOption(searchFlagOpt);
        AddOption(elevatedOpt);
        AddOption(viewOpt);

        // --- Register Subcommands ---
        AddCommand(new AddCommand(storageService, console));
        AddCommand(new UpdateCommand(storageService, console));
        AddCommand(new DeleteCommand(storageService, console));
        AddCommand(new ListCommand(storageService, console));
        AddCommand(new ViewCommand(storageService, console));
        AddCommand(new DocsCommand(configService, console));
        AddCommand(new BackupCommand(configService, storageService, console));
        AddCommand(new ExportCommand(storageService, console));
        AddCommand(new ImportCommand(storageService, console));

        // Config subcommands structure
        var configCommand = new ConfigCommand();
        configCommand.AddCommand(new ShowCommand(configService, console));
        configCommand.AddCommand(new SetLocationCommand(configService, console));
        configCommand.AddCommand(new SetEncryptionCommand(configService, credentialService, storageService, console));
        configCommand.AddCommand(new ClearPasswordCommand(credentialService, console));
        configCommand.AddCommand(new SetUrlAliasesCommand(configService, console));
        configCommand.AddCommand(new ClearUrlAliasesCommand(configService, console));
        configCommand.AddCommand(new ClearUrlAliasCommand(configService, console));
        configCommand.AddCommand(new SetDefaultParamsCommand(configService, console));
        configCommand.AddCommand(new ClearDefaultParamsCommand(configService, console));
        configCommand.AddCommand(new ClearDefaultParamCommand(configService, console));

        var setProviderCommand = new SetProviderCommand();
        setProviderCommand.AddCommand(new SmtpCommand(storageService, console));
        setProviderCommand.AddCommand(new GraphCommand(storageService, graphAuthService, console));
        configCommand.AddCommand(setProviderCommand);

        configCommand.AddCommand(new AuthGraphCommand(graphAuthService));

        AddCommand(configCommand);

        // Implicit handler
        this.SetHandler(async (string keyResult, string[] actArgs, bool returnValue, bool forceCopy, bool searchFlag, bool elevated, bool viewFlag) =>
        {
            try
            {
                var keys = storageService.GetKeys().Where(x => x != null && !x.Key.StartsWith("__")).ToList();
                OKey? foundKey = null;

                if (string.IsNullOrEmpty(keyResult))
                {
                    foundKey = InteractiveKeyPicker.Pick(_console, keys, "Select a key to run");
                    if (foundKey == null)
                    {
                        return;
                    }
                }
                else if (searchFlag)
                {
                    var matches = keys.Where(k => k != null && (
                        (!string.IsNullOrWhiteSpace(k.Key) && k.Key.Contains(keyResult, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(k.Description) && k.Description.Contains(keyResult, StringComparison.OrdinalIgnoreCase))
                    )).ToList();

                    if (matches.Count == 0)
                    {
                        _console.MarkupLine($"[yellow]No keys found matching '{keyResult}'.[/]");
                        return;
                    }
                    else if (matches.Count > 1)
                    {
                        foundKey = InteractiveKeyPicker.Pick(_console, matches, $"Multiple matches for '{keyResult}' - pick one");
                        if (foundKey == null)
                        {
                            return;
                        }
                    }
                    else
                    {
                        foundKey = matches.First();
                    }
                }
                else
                {
                    foundKey = keys.FirstOrDefault(k => k?.Key != null && k.Key.Equals(keyResult, StringComparison.OrdinalIgnoreCase));
                    if (foundKey == null)
                    {
                        _console.MarkupLine($"[red]Key '{keyResult}' not found.[/]");
                        return;
                    }
                }

                if (viewFlag)
                {
                    CommandHelpers.DisplayKey(foundKey, _console);
                    return;
                }

                await actionService.ExecuteAsync(foundKey, actArgs, returnValue, forceCopy, elevated);
            }
            catch (Exception ex)
            {
                _console.MarkupLine($"[red]Error:[/] {ex.Message}");
                if (ex.InnerException != null) 
                {
                    _console.MarkupLine($"[dim]{ex.InnerException.Message}[/]");
                    if (ex.InnerException.Message.Contains("Failed to decrypt"))
                    {
                        _console.MarkupLine("[yellow]Hint:[/] Are you using the correct encryption mode (Local vs Portable)?");
                    }
                }
            }
        }, keyArgument, actArgsArgument, returnOpt, copyOpt, searchFlagOpt, elevatedOpt, viewOpt);
    }
}
