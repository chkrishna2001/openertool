using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Opener.Models;
using Opener.Services;
using Spectre.Console;

namespace Opener;

class Program
{
    private const string UrlAliasJsonHelp = "Inline JSON or a JSON file path. Shape: { \"placeholder\": { \"input\": \"replacement\" } }. Example file content: { \"env\": { \"d\": \"-dev\", \"u\": \"-uat\", \"p\": \"\" } }";
    private const string DefaultParamsJsonHelp = "Inline JSON or a JSON file path. Shape: { \"placeholder\": \"defaultValue\" }. Example file content: { \"user\": \"kchirravuri\", \"region\": \"us\" }";

    static async Task Main(string[] args)
    {
        // 1. Setup Services
        var configService = new ConfigService();
        
        // Only Windows supported for Credential Manager currently, fallback to File on Linux
        ICredentialService credentialService = CredentialServiceFactory.Create(); 
        
        IEncryptionService encryptionService = null!;

        try 
        {
            encryptionService = EncryptionServiceFactory.Create(configService, credentialService);
        }
        catch (InvalidOperationException)
        {
            // Password missing in portable mode. Prompt for it.
            if (configService.IsPortableMode())
            {
                // We handle this gracefully: prompt user, save to session (CredentialManager is persistent though)
                // If the user cleared it, they must re-enter.
                AnsiConsole.MarkupLine("[yellow]Portable mode enabled. Password required.[/]");
                var pass = AnsiConsole.Prompt(new TextPrompt<string>("Enter Password:").Secret());
                credentialService.SetPassword(pass);
                encryptionService = EncryptionServiceFactory.Create(configService, credentialService);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Encryption initialization failed.[/]");
                return;
            }
        }

        var storageService = new StorageService(configService, encryptionService);
        var actionService = new ActionService(configService);

        // 1.5 Auto-initialize storage
        try 
        {
            storageService.Initialize();
        }
        catch (Exception ex)
        {
            // Log the initialization warning but don't crash
            // This allows users to run 'config set-location' to fix a broken storage path
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Storage initialization failed: {ex.Message}");
            AnsiConsole.MarkupLine("[yellow]Use 'opener config set-location <path>' to change storage location if needed.[/]");
        }

        // 2. Define Commands
        var rootCommand = new RootCommand(
            "Opener Tool - quickly open links, paths, stored data, and REST shortcuts.\n\n" +
            "Examples:\n" +
            "  o list\n" +
            "  o list -s github\n" +
            "  o add jira \"https://jira.company.com/browse/{0}\" -t WebPath\n" +
            "  o jira PROJ-123\n" +
            "  o jira -s    # search for keys containing 'jira' and execute if single match\n" +
            "  o githubtoken -r  # print token to stdout instead of copying\n" +
            "  o githubtoken -c  # force copy token to clipboard instead of opening\n" +
            "  o add api \"https://nexus<env>.bpc.com/<region>/<user>\" -t WebPath --url-aliases '{ \"env\": { \"d\": \"-dev\", \"p\": \"\" } }' --default-params '{ \"user\": \"kchirravuri\" }'\n" +
            "  o api env=d region=us");

        // --- Arguments for Implicit Key ---
        var keyArgument = new Argument<string>("key", "Stored key to execute. Use 'o list' to see available keys.") { Arity = ArgumentArity.ZeroOrOne, IsHidden = true };
        var actArgsArgument = new Argument<string[]>("args", "Arguments passed to the key action. URL templates accept positional values or named values like env=d region=us.") { Arity = ArgumentArity.ZeroOrMore, IsHidden = true };
        rootCommand.AddArgument(keyArgument);
        rootCommand.AddArgument(actArgsArgument);
        
        // Global options for implicit execution
        var returnOpt = new Option<bool>(new[] { "-r", "--return" }, "Return the resolved value to stdout instead of copying/opening");
        var copyOpt = new Option<bool>(new[] { "-c", "--copy" }, "Force copy the resolved value to clipboard instead of performing the default action");
        var searchFlagOpt = new Option<bool>(new[] { "-s", "--search" }, "Treat the provided key as a search term and lookup by substring (key + description)");
        rootCommand.AddOption(returnOpt);
        rootCommand.AddOption(copyOpt);
        rootCommand.AddOption(searchFlagOpt);
        // --- CONFIG Command ---
        var configCommand = new Command(
            "config",
            "Manage storage, encryption, and global URL template settings.\n\n" +
            "Examples:\n" +
            "  o config show\n" +
            "  o config set-encryption portable --password my-secret\n" +
            "  o config set-url-aliases env d=-dev u=-uat p=\n" +
            "  o config set-url-aliases --file aliases.json\n" +
            "  o config set-default-params user kchirravuri");
        
        var showConfig = new Command("show", "Show current storage path, encryption mode, global URL aliases, and global default params.");
        showConfig.SetHandler(() => 
        {
            var conf = configService.GetConfig();
            AnsiConsole.MarkupLine($"[bold]Storage Location:[/] {configService.GetDataFilePath()}");
            AnsiConsole.MarkupLine($"[bold]Encryption Mode:[/] {conf.EncryptionMode}");
            AnsiConsole.MarkupLine("[bold]Global URL Aliases:[/]");
            Console.WriteLine(JsonSerializer.Serialize(conf.GlobalUrlAliases, OpenerJsonContext.Default.DictionaryStringDictionaryStringString));
            AnsiConsole.MarkupLine("[bold]Global Default Params:[/]");
            Console.WriteLine(JsonSerializer.Serialize(conf.GlobalDefaultParams, OpenerJsonContext.Default.DictionaryStringString));
            if (conf.EncryptionMode == "portable")
            {
                AnsiConsole.MarkupLine("[dim](Password is cached in Windows Credential Manager)[/]");
            }
        });
        configCommand.AddCommand(showConfig);

        var setLocation = new Command(
            "set-location",
            "Set a custom encrypted data-file path.\n\n" +
            "Example:\n" +
            "  o config set-location \"C:\\Users\\me\\OneDrive\\opener.dat\"");
        var pathArg = new Argument<string>("path", "Full path to opener.dat, including the file name.");
        setLocation.AddArgument(pathArg);
        setLocation.SetHandler((string path) => 
        {
            var conf = configService.GetConfig();
            // If changing location, attempt to move file? 
            // For now, simple switch. User can manually move or use export/import.
            // Better UX: Ask to move?
            // Let's keep it simple: Just set path.
            conf.StorageLocation = path;
            configService.SaveConfig(conf);
            AnsiConsole.MarkupLine($"[green]Storage location updated to:[/] {path}");
            AnsiConsole.MarkupLine("[yellow]Note: Existing keys were not moved. Use 'export' then 'import' if needed.[/]");
        }, pathArg);
        configCommand.AddCommand(setLocation);

        var setEncryption = new Command(
            "set-encryption",
            "Switch between local machine encryption and portable password encryption.\n\n" +
            "Examples:\n" +
            "  o config set-encryption local\n" +
            "  o config set-encryption portable --password my-secret");
        var modeArg = new Argument<string>("mode", "Encryption mode: local or portable.");
        var setEncPassOpt = new Option<string>(new[] { "-p", "--password" }, "Portable-mode password. Omit it to be prompted interactively.");
        setEncryption.AddArgument(modeArg);
        setEncryption.AddOption(setEncPassOpt);
        setEncryption.SetHandler((string mode, string? passInput) => 
        {
            mode = mode.ToLower();
            if (mode != "local" && mode != "portable") 
            {
                AnsiConsole.MarkupLine("[red]Invalid mode. Use 'local' or 'portable'.[/]");
                return;
            }

            var conf = configService.GetConfig();
            if (conf.EncryptionMode == mode)
            {
                AnsiConsole.MarkupLine($"[yellow]Already in {mode} mode.[/]");
                return;
            }

            // MIGRATION LOGIC
            // 1. Decrypt current keys using CURRENT service
            var currentKeys = storageService.GetKeys();
            
            // 2. Prepare NEW configuration
            if (mode == "portable")
            {
                string pass = passInput ?? string.Empty;
                if (string.IsNullOrEmpty(pass))
                {
                    pass = AnsiConsole.Prompt(new TextPrompt<string>("Set Portable Password:").Secret());
                    var confirm = AnsiConsole.Prompt(new TextPrompt<string>("Confirm Password:").Secret());
                    if (pass != confirm) 
                    {
                        AnsiConsole.MarkupLine("[red]Passwords do not match.[/]");
                        return;
                    }
                }
                credentialService.SetPassword(pass);
            }
            else // switching to local
            {
                credentialService.ClearPassword();
            }

            // 3. Save config
            conf.EncryptionMode = mode;
            configService.SaveConfig(conf);

            // 4. Re-initialize services with NEW config to save keys
            try 
            {
                var newEncryptor = EncryptionServiceFactory.Create(configService, credentialService);
                var newStorage = new StorageService(configService, newEncryptor);
                newStorage.SaveKeys(currentKeys);
                AnsiConsole.MarkupLine($"[green]Switched to {mode} mode and re-encrypted {currentKeys.Count} keys.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Migration failed:[/] {ex.Message}");
                // Revert config?
                // For safety, user should backup first.
            }

        }, modeArg, setEncPassOpt);
        configCommand.AddCommand(setEncryption);
        
        var clearPass = new Command("clear-password", "Clear the cached portable-mode password from the local credential store.");
        clearPass.SetHandler(() => 
        {
            credentialService.ClearPassword();
            AnsiConsole.MarkupLine("[green]Cached password cleared.[/]");
        });
        configCommand.AddCommand(clearPass);

        var setUrlAliases = new Command(
            "set-url-aliases",
            "Set a global URL alias map for one placeholder, or replace all global alias maps from a JSON file.\n\n" +
            "Aliases translate compact input values before a URL template is opened. For a template containing <env>, this can turn 'd' into '-dev' or 'p' into an empty production suffix.\n\n" +
            "Examples:\n" +
            "  o config set-url-aliases env d=-dev u=-uat p=\n" +
            "  o config set-url-aliases region us=na eu=emea\n" +
            "  o config set-url-aliases --file aliases.json");
        var urlAliasesPlaceholderArg = new Argument<string?>("placeholder", "Named placeholder without angle brackets, for example env for <env>. Omit when using --file.") { Arity = ArgumentArity.ZeroOrOne };
        var urlAliasesPairsArg = new Argument<string[]>("aliases", "Alias pairs in input=replacement form. Empty replacements are allowed, for example p=.") { Arity = ArgumentArity.ZeroOrMore };
        var urlAliasesFileOpt = new Option<string?>(new[] { "--file" }, "Path to a JSON file containing all global URL alias maps.");
        setUrlAliases.AddArgument(urlAliasesPlaceholderArg);
        setUrlAliases.AddArgument(urlAliasesPairsArg);
        setUrlAliases.AddOption(urlAliasesFileOpt);
        setUrlAliases.SetHandler((string? placeholder, string[] pairs, string? file) =>
        {
            var conf = configService.GetConfig();

            if (!string.IsNullOrWhiteSpace(file))
            {
                try
                {
                    conf.GlobalUrlAliases = JsonSerializer.Deserialize(ReadJsonFile(file), OpenerJsonContext.Default.DictionaryStringDictionaryStringString)
                        ?? new(StringComparer.OrdinalIgnoreCase);
                    configService.SaveConfig(conf);
                    AnsiConsole.MarkupLine("[green]Global URL aliases updated.[/]");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    AnsiConsole.MarkupLine($"[red]Unable to read alias JSON file:[/] {ex.Message}");
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(placeholder))
            {
                AnsiConsole.MarkupLine("[red]Missing placeholder.[/] Use 'o config set-url-aliases env d=-dev' or 'o config set-url-aliases --file aliases.json'.");
                return;
            }

            if (pairs.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]Missing alias pairs.[/] Use input=replacement pairs, for example d=-dev u=-uat p=.");
                return;
            }

            if (!TryParsePairs(pairs, out var aliases, out var error))
            {
                AnsiConsole.MarkupLine($"[red]Invalid alias pair:[/] {error}");
                return;
            }

            conf.GlobalUrlAliases[placeholder] = aliases;
            configService.SaveConfig(conf);
            AnsiConsole.MarkupLine($"[green]Global URL aliases updated for <{placeholder}>.[/]");
        }, urlAliasesPlaceholderArg, urlAliasesPairsArg, urlAliasesFileOpt);
        configCommand.AddCommand(setUrlAliases);

        var clearUrlAliases = new Command("clear-url-aliases", "Clear all global URL alias maps. Per-key aliases are not changed.");
        clearUrlAliases.SetHandler(() =>
        {
            var conf = configService.GetConfig();
            conf.GlobalUrlAliases.Clear();
            configService.SaveConfig(conf);
            AnsiConsole.MarkupLine("[green]Global URL aliases cleared.[/]");
        });
        configCommand.AddCommand(clearUrlAliases);

        var clearUrlAlias = new Command("clear-url-alias", "Clear one global URL alias map by placeholder name.");
        var clearAliasNameArg = new Argument<string>("placeholder", "Named placeholder without angle brackets, for example env for <env>.");
        clearUrlAlias.AddArgument(clearAliasNameArg);
        clearUrlAlias.SetHandler((string placeholder) =>
        {
            var conf = configService.GetConfig();
            if (conf.GlobalUrlAliases.Remove(placeholder))
            {
                configService.SaveConfig(conf);
                AnsiConsole.MarkupLine($"[green]Global URL aliases cleared for <{placeholder}>.[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[yellow]No global URL aliases found for <{placeholder}>.[/]");
        }, clearAliasNameArg);
        configCommand.AddCommand(clearUrlAlias);

        var setDefaultParams = new Command(
            "set-default-params",
            "Set one global default value, or replace all global defaults from a JSON file.\n\n" +
            "Defaults are used when a named placeholder is not supplied by positional args or key=value args.\n\n" +
            "Examples:\n" +
            "  o config set-default-params user kchirravuri\n" +
            "  o config set-default-params region us\n" +
            "  o config set-default-params --file defaults.json");
        var defaultPlaceholderArg = new Argument<string?>("placeholder", "Named placeholder without angle brackets, for example user for <user>. Omit when using --file.") { Arity = ArgumentArity.ZeroOrOne };
        var defaultValueArg = new Argument<string?>("value", "Default value to use when the placeholder is omitted. Omit when using --file.") { Arity = ArgumentArity.ZeroOrOne };
        var defaultParamsFileOpt = new Option<string?>(new[] { "--file" }, "Path to a JSON file containing all global default params.");
        setDefaultParams.AddArgument(defaultPlaceholderArg);
        setDefaultParams.AddArgument(defaultValueArg);
        setDefaultParams.AddOption(defaultParamsFileOpt);
        setDefaultParams.SetHandler((string? placeholder, string? value, string? file) =>
        {
            var conf = configService.GetConfig();

            if (!string.IsNullOrWhiteSpace(file))
            {
                try
                {
                    conf.GlobalDefaultParams = JsonSerializer.Deserialize(ReadJsonFile(file), OpenerJsonContext.Default.DictionaryStringString)
                        ?? new(StringComparer.OrdinalIgnoreCase);
                    configService.SaveConfig(conf);
                    AnsiConsole.MarkupLine("[green]Global default params updated.[/]");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    AnsiConsole.MarkupLine($"[red]Unable to read default params JSON file:[/] {ex.Message}");
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(placeholder) || value == null)
            {
                AnsiConsole.MarkupLine("[red]Missing placeholder or value.[/] Use 'o config set-default-params user kchirravuri' or 'o config set-default-params --file defaults.json'.");
                return;
            }

            conf.GlobalDefaultParams[placeholder] = value;
            configService.SaveConfig(conf);
            AnsiConsole.MarkupLine($"[green]Global default param updated for <{placeholder}>.[/]");
        }, defaultPlaceholderArg, defaultValueArg, defaultParamsFileOpt);
        configCommand.AddCommand(setDefaultParams);

        var clearDefaultParams = new Command("clear-default-params", "Clear all global default placeholder values. Per-key defaults are not changed.");
        clearDefaultParams.SetHandler(() =>
        {
            var conf = configService.GetConfig();
            conf.GlobalDefaultParams.Clear();
            configService.SaveConfig(conf);
            AnsiConsole.MarkupLine("[green]Global default params cleared.[/]");
        });
        configCommand.AddCommand(clearDefaultParams);

        var clearDefaultParam = new Command("clear-default-param", "Clear one global default value by placeholder name.");
        var clearDefaultNameArg = new Argument<string>("placeholder", "Named placeholder without angle brackets, for example user for <user>.");
        clearDefaultParam.AddArgument(clearDefaultNameArg);
        clearDefaultParam.SetHandler((string placeholder) =>
        {
            var conf = configService.GetConfig();
            if (conf.GlobalDefaultParams.Remove(placeholder))
            {
                configService.SaveConfig(conf);
                AnsiConsole.MarkupLine($"[green]Global default param cleared for <{placeholder}>.[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[yellow]No global default param found for <{placeholder}>.[/]");
        }, clearDefaultNameArg);
        configCommand.AddCommand(clearDefaultParam);

        rootCommand.AddCommand(configCommand);

        // --- EXPORT/IMPORT ---
        var exportCommand = new Command(
            "export",
            "Export all keys to a portable encrypted backup file.\n\n" +
            "Example:\n" +
            "  o export backup.dat --password my-export-password");
        var exportPathArg = new Argument<string>("file", "Output encrypted backup file path.");
        var exportPassOpt = new Option<string>(new[] { "-p", "--password" }, "Password for the export file. Omit it to be prompted interactively.");
        exportCommand.AddArgument(exportPathArg);
        exportCommand.AddOption(exportPassOpt);
        exportCommand.SetHandler((string path, string? passInput) => 
        {
            var keys = storageService.GetKeys();
            if (keys.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No keys to export.[/]");
                return;
            }

            string pass = passInput ?? string.Empty;
            if (string.IsNullOrEmpty(pass))
            {
                pass = AnsiConsole.Prompt(new TextPrompt<string>("Enter password for export file:").Secret());
                var confirm = AnsiConsole.Prompt(new TextPrompt<string>("Confirm password:").Secret());
                if (pass != confirm) 
                {
                    AnsiConsole.MarkupLine("[red]Passwords do not match.[/]");
                    return;
                }
            }

            var tempEncryptor = new PortableEncryptionService(pass);
            // Verify path
            try {
                var json = JsonSerializer.Serialize(keys, OpenerJsonContext.Default.ListOKey);
                var encrypted = tempEncryptor.Encrypt(json);
                File.WriteAllText(path, encrypted);
                AnsiConsole.MarkupLine($"[green]Exported {keys.Count} keys to {path}[/]");
            } catch (Exception ex) {
                AnsiConsole.MarkupLine($"[red]Export failed:[/] {ex.Message}");
            }
        }, exportPathArg, exportPassOpt);
        rootCommand.AddCommand(exportCommand);

        var importCommand = new Command(
            "import",
            "Import keys from a portable encrypted backup file. Existing keys with the same name are updated.\n\n" +
            "Example:\n" +
            "  o import backup.dat --password my-export-password");
        var importPathArg = new Argument<string>("file", "Input encrypted backup file path.");
        var importPassOpt = new Option<string>(new[] { "-p", "--password" }, "Password for the import file. Omit it to be prompted interactively.");
        importCommand.AddArgument(importPathArg);
        importCommand.AddOption(importPassOpt);
        importCommand.SetHandler((string path, string? passInput) => 
        {
            if (!File.Exists(path))
            {
                AnsiConsole.MarkupLine($"[red]File not found: {path}[/]");
                return;
            }

            string pass = passInput ?? string.Empty;
            if (string.IsNullOrEmpty(pass))
            {
                pass = AnsiConsole.Prompt(new TextPrompt<string>("Enter password for import file:").Secret());
            }
            
            var tempEncryptor = new PortableEncryptionService(pass);

            try 
            {
                var content = File.ReadAllText(path);
                var json = tempEncryptor.Decrypt(content);
                var importedKeys = JsonSerializer.Deserialize(json, OpenerJsonContext.Default.ListOKey);

                if (importedKeys != null)
                {
                    var currentKeys = storageService.GetKeys();
                    int added = 0;
                    int updated = 0;
                    foreach(var k in importedKeys)
                    {
                        var existing = currentKeys.FirstOrDefault(x => x?.Key != null && x.Key.Equals(k.Key, StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                        {
                            existing.Value = k.Value;
                            existing.KeyType = k.KeyType;
                            updated++;
                        }
                        else
                        {
                            currentKeys.Add(k);
                            added++;
                        }
                    }
                    storageService.SaveKeys(currentKeys);
                    AnsiConsole.MarkupLine($"[green]Import successful. Added: {added}, Updated: {updated}[/]");
                }
            }
            catch (Exception ex)
            {
                 AnsiConsole.MarkupLine($"[red]Import failed (Wrong password?):[/] {ex.Message}");
            }

        }, importPathArg, importPassOpt);
        rootCommand.AddCommand(importCommand);

        // --- EXISTING COMMANDS ---

        var addCommand = new Command(
            "add",
            "Add a new key.\n\n" +
            "Examples:\n" +
            "  o add token \"secret-value\" -t Data\n" +
            "  o add jira \"https://jira.company.com/browse/{0}\" -t WebPath\n" +
            "  o add api \"https://nexus<env>.bpc.com/<region>/<user>\" -t WebPath\n" +
            "  o config set-url-aliases env d=-dev u=-uat p=\n" +
            "  o config set-default-params user kchirravuri");
        var addKeyArg = new Argument<string>("key", "Unique key name, for example jira or api.");
        var addValArg = new Argument<string>("value", "Stored value. Meaning depends on --type: URL template, local path, data, JSON, or REST JSON.");
        var addTypeOpt = new Option<OKeyType>(new[] { "-t", "--type" }, () => OKeyType.Data, "Key type: WebPath, LocalPath, Data, JsonData, or Rest.");
        var addUrlAliasesOpt = new Option<string?>(new[] { "--url-aliases" }, "Per-key alias map JSON. " + UrlAliasJsonHelp);
        var addDefaultParamsOpt = new Option<string?>(new[] { "--default-params" }, "Per-key default params JSON. " + DefaultParamsJsonHelp);
        addCommand.AddArgument(addKeyArg);
        addCommand.AddArgument(addValArg);
        addCommand.AddOption(addTypeOpt);
        addCommand.AddOption(addUrlAliasesOpt);
        addCommand.AddOption(addDefaultParamsOpt);
        addCommand.SetHandler((string k, string v, OKeyType t, string? urlAliasesJson, string? defaultParamsJson) =>
        {
            var keys = storageService.GetKeys();
            if (keys.Any(x => x?.Key != null && x.Key.Equals(k, StringComparison.OrdinalIgnoreCase)))
            {
                AnsiConsole.MarkupLine($"[red]Key '{k}' already exists. Use update command.[/]");
                return;
            }
            var newKey = new OKey { Key = k ?? string.Empty, Value = v ?? string.Empty, KeyType = t };

            var resolvedUrlAliasesJson = ResolveJsonInput(urlAliasesJson);
            if (!string.IsNullOrWhiteSpace(resolvedUrlAliasesJson))
            {
                try { newKey.UrlAliases = JsonSerializer.Deserialize(resolvedUrlAliasesJson, OpenerJsonContext.Default.DictionaryStringDictionaryStringString) ?? newKey.UrlAliases; }
                catch { AnsiConsole.MarkupLine("[yellow]Warning:[/] Invalid JSON for --url-aliases, ignoring."); }
            }

            var resolvedDefaultParamsJson = ResolveJsonInput(defaultParamsJson);
            if (!string.IsNullOrWhiteSpace(resolvedDefaultParamsJson))
            {
                try { newKey.DefaultParams = JsonSerializer.Deserialize(resolvedDefaultParamsJson, OpenerJsonContext.Default.DictionaryStringString) ?? newKey.DefaultParams; }
                catch { AnsiConsole.MarkupLine("[yellow]Warning:[/] Invalid JSON for --default-params, ignoring."); }
            }

            keys.Add(newKey);
            storageService.SaveKeys(keys);
            AnsiConsole.MarkupLine($"[green]Key '{k}' added successfully![/]");
        }, addKeyArg, addValArg, addTypeOpt, addUrlAliasesOpt, addDefaultParamsOpt);
        rootCommand.AddCommand(addCommand);

        // UPDATE
        var updateCommand = new Command(
            "update",
            "Update an existing key's value and optionally replace its per-key URL aliases/default params.\n\n" +
            "Examples:\n" +
            "  o update jira \"https://jira.company.com/browse/{0}\"\n" +
            "  o update api \"https://nexus<env>.bpc.com/<region>/<user>\"\n" +
            "  o config set-url-aliases env d=-dev u=-uat p=");
        var upKeyArg = new Argument<string>("key", "Existing key name to update.");
        var upValArg = new Argument<string>("value", "Replacement stored value.");
        var upUrlAliasesOpt = new Option<string?>(new[] { "--url-aliases" }, "Replace per-key alias map JSON. " + UrlAliasJsonHelp);
        var upDefaultParamsOpt = new Option<string?>(new[] { "--default-params" }, "Replace per-key default params JSON. " + DefaultParamsJsonHelp);
        updateCommand.AddArgument(upKeyArg);
        updateCommand.AddArgument(upValArg);
        updateCommand.AddOption(upUrlAliasesOpt);
        updateCommand.AddOption(upDefaultParamsOpt);
        updateCommand.SetHandler((string k, string v, string? urlAliasesJson, string? defaultParamsJson) =>
        {
            var keys = storageService.GetKeys();
            var existing = keys.FirstOrDefault(x => x?.Key != null && x.Key.Equals(k, StringComparison.OrdinalIgnoreCase));
            if (existing == null) { AnsiConsole.MarkupLine($"[red]Key '{k}' not found.[/]"); return; }
            existing.Value = v ?? string.Empty;
            var resolvedUrlAliasesJson = ResolveJsonInput(urlAliasesJson);
            if (!string.IsNullOrWhiteSpace(resolvedUrlAliasesJson))
            {
                try { existing.UrlAliases = JsonSerializer.Deserialize(resolvedUrlAliasesJson, OpenerJsonContext.Default.DictionaryStringDictionaryStringString) ?? existing.UrlAliases; }
                catch { AnsiConsole.MarkupLine("[yellow]Warning:[/] Invalid JSON for --url-aliases, ignoring."); }
            }

            var resolvedDefaultParamsJson = ResolveJsonInput(defaultParamsJson);
            if (!string.IsNullOrWhiteSpace(resolvedDefaultParamsJson))
            {
                try { existing.DefaultParams = JsonSerializer.Deserialize(resolvedDefaultParamsJson, OpenerJsonContext.Default.DictionaryStringString) ?? existing.DefaultParams; }
                catch { AnsiConsole.MarkupLine("[yellow]Warning:[/] Invalid JSON for --default-params, ignoring."); }
            }

            storageService.SaveKeys(keys);
            AnsiConsole.MarkupLine($"[green]Key '{k}' updated successfully![/]");
        }, upKeyArg, upValArg, upUrlAliasesOpt, upDefaultParamsOpt);
        rootCommand.AddCommand(updateCommand);

        // DELETE
        var deleteCommand = new Command(
            "delete",
            "Delete a stored key.\n\n" +
            "Example:\n" +
            "  o delete jira");
        var delKeyArg = new Argument<string>("key", "Existing key name to delete.");
        deleteCommand.AddArgument(delKeyArg);
        deleteCommand.SetHandler((string k) =>
        {
            var keys = storageService.GetKeys();
            var existing = keys.FirstOrDefault(x => x?.Key != null && x.Key.Equals(k, StringComparison.OrdinalIgnoreCase));
            if (existing == null) { AnsiConsole.MarkupLine($"[red]Key '{k}' not found.[/]"); return; }
            keys.Remove(existing);
            storageService.SaveKeys(keys);
            AnsiConsole.MarkupLine($"[green]Key '{k}' deleted.[/]");
        }, delKeyArg);
        rootCommand.AddCommand(deleteCommand);

        // LIST
        var listCommand = new Command("list", "List all stored keys with type and a safe value preview.");
        var listSearchOpt = new Option<string?>(new[] { "-s", "--search" }, "Filter keys by case-insensitive substring across key and description.") { Arity = ArgumentArity.ZeroOrOne };
        listCommand.AddOption(listSearchOpt);
        listCommand.SetHandler((string? search) =>
        {
            var keys = storageService.GetKeys();
            if (keys == null || keys.Count == 0) { AnsiConsole.MarkupLine("[yellow]No keys found.[/]"); return; }
            var filtered = keys.Where(x => x != null).ToList();
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(k => (!string.IsNullOrWhiteSpace(k.Key) && k.Key.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                                             (!string.IsNullOrWhiteSpace(k.Description) && k.Description.Contains(search, StringComparison.OrdinalIgnoreCase))).OrderBy(x => x.Key).ToList();
            }
            if (filtered.Count == 0) { AnsiConsole.MarkupLine("[yellow]No keys match the search term.[/]"); return; }
            var table = new Table();
            table.AddColumn("Key");
            table.AddColumn("Type");
            table.AddColumn("Value (Preview)");
            foreach (var key in filtered.OrderBy(x => x.Key))
            {
                string valPreview = (key.Value ?? string.Empty).Length > 50 ? key.Value!.Substring(0, 47) + "..." : (key.Value ?? string.Empty);
                if(key.KeyType == OKeyType.Data || key.KeyType == OKeyType.JsonData) valPreview = "********";
                table.AddRow(key.Key ?? "N/A", key.KeyType.ToString(), Markup.Escape(valPreview));
            }
            AnsiConsole.Write(table);
        }, listSearchOpt);
        rootCommand.AddCommand(listCommand);

        // Implicit handler
        rootCommand.SetHandler(async (string keyResult, string[] actArgs, bool returnValue, bool forceCopy, bool searchFlag) =>
        {
            if (string.IsNullOrEmpty(keyResult))
            {
                AnsiConsole.MarkupLine("Opener Tool - Use [bold]o --help[/] for details.");
                return; 
            }
            
            try 
            {
                var keys = storageService.GetKeys();
                OKey? foundKey = null;
                if (searchFlag)
                {
                    var matches = keys.Where(k => k != null && (
                        (!string.IsNullOrWhiteSpace(k.Key) && k.Key.Contains(keyResult, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(k.Description) && k.Description.Contains(keyResult, StringComparison.OrdinalIgnoreCase))
                    )).ToList();

                    if (matches.Count == 0)
                    {
                        AnsiConsole.MarkupLine($"[yellow]No keys found matching '{keyResult}'.[/]");
                        return;
                    }
                    else if (matches.Count > 1)
                    {
                        AnsiConsole.MarkupLine($"[yellow]Multiple matches found for '{keyResult}':[/]");
                        int i = 1;
                        foreach (var m in matches)
                        {
                            AnsiConsole.MarkupLine($"{i++}. {m.Key} - {m.Description}");
                        }
                        return;
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
                        AnsiConsole.MarkupLine($"[red]Key '{keyResult}' not found.[/]");
                        return;
                    }
                }

                await actionService.ExecuteAsync(foundKey, actArgs, returnValue, forceCopy);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                if (ex.InnerException != null) 
                {
                    AnsiConsole.MarkupLine($"[dim]{ex.InnerException.Message}[/]");
                    if (ex.InnerException.Message.Contains("Failed to decrypt"))
                    {
                        AnsiConsole.MarkupLine("[yellow]Hint:[/] Are you using the correct encryption mode (Local vs Portable)?");
                    }
                }
            }
        }, keyArgument, actArgsArgument, returnOpt, copyOpt, searchFlagOpt);

        await new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .Build()
            .InvokeAsync(args);
    }

    private static bool TryParsePairs(string[] pairs, out Dictionary<string, string> parsed, out string error)
    {
        parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;

        foreach (var pair in pairs)
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0)
            {
                error = $"{pair}. Use input=replacement, for example d=-dev or p=.";
                return false;
            }

            var key = pair.Substring(0, separatorIndex).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                error = $"{pair}. Alias input cannot be empty.";
                return false;
            }

            parsed[key] = pair.Substring(separatorIndex + 1);
        }

        return true;
    }

    private static string ResolveJsonInput(string? jsonOrFile)
    {
        if (string.IsNullOrWhiteSpace(jsonOrFile))
        {
            return string.Empty;
        }

        if (File.Exists(jsonOrFile))
        {
            return ReadJsonFile(jsonOrFile);
        }

        return jsonOrFile;
    }

    private static string ReadJsonFile(string path)
    {
        return File.ReadAllText(path);
    }
}
