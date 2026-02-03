using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Linq;
using System.Threading.Tasks;
using Opener.Models;
using Opener.Services;
using Spectre.Console;

namespace Opener;

class Program
{
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
        var actionService = new ActionService();

        // 1.5 Auto-initialize storage
        storageService.Initialize();

        // 2. Define Commands
        var rootCommand = new RootCommand("Opener Tool - Quickly open links, paths, and data.");

        // --- Arguments for Implicit Key ---
        var keyArgument = new Argument<string>("key", "The key to act upon") { Arity = ArgumentArity.ZeroOrOne };
        var actArgsArgument = new Argument<string[]>("args", "Arguments for the action") { Arity = ArgumentArity.ZeroOrMore };
        rootCommand.AddArgument(keyArgument);
        rootCommand.AddArgument(actArgsArgument);

        // --- CONFIG Command ---
        var configCommand = new Command("config", "Manage configuration");
        
        var showConfig = new Command("show", "Show current configuration");
        showConfig.SetHandler(() => 
        {
            var conf = configService.GetConfig();
            AnsiConsole.MarkupLine($"[bold]Storage Location:[/] {configService.GetDataFilePath()}");
            AnsiConsole.MarkupLine($"[bold]Encryption Mode:[/] {conf.EncryptionMode}");
            if (conf.EncryptionMode == "portable")
            {
                AnsiConsole.MarkupLine("[dim](Password is cached in Windows Credential Manager)[/]");
            }
        });
        configCommand.AddCommand(showConfig);

        var setLocation = new Command("set-location", "Set custom storage location");
        var pathArg = new Argument<string>("path", "Full path to opener.dat file (e.g. OneDrive path)");
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

        var setEncryption = new Command("set-encryption", "Set encryption mode (local/portable)");
        var modeArg = new Argument<string>("mode", "local or portable");
        setEncryption.AddArgument(modeArg);
        setEncryption.SetHandler((string mode) => 
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
                var pass = AnsiConsole.Prompt(new TextPrompt<string>("Set Portable Password:").Secret());
                var confirm = AnsiConsole.Prompt(new TextPrompt<string>("Confirm Password:").Secret());
                if (pass != confirm) 
                {
                    AnsiConsole.MarkupLine("[red]Passwords do not match.[/]");
                    return;
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

        }, modeArg);
        configCommand.AddCommand(setEncryption);
        
        var clearPass = new Command("clear-password", "Clear cached portable password");
        clearPass.SetHandler(() => 
        {
            credentialService.ClearPassword();
            AnsiConsole.MarkupLine("[green]Cached password cleared.[/]");
        });
        configCommand.AddCommand(clearPass);

        rootCommand.AddCommand(configCommand);

        // --- EXPORT/IMPORT ---
        var exportCommand = new Command("export", "Export keys to a portable encrypted file");
        var exportPathArg = new Argument<string>("file", "Output file path");
        exportCommand.AddArgument(exportPathArg);
        exportCommand.SetHandler((string path) => 
        {
            var keys = storageService.GetKeys();
            if (keys.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No keys to export.[/]");
                return;
            }

            var pass = AnsiConsole.Prompt(new TextPrompt<string>("Enter password for export file:").Secret());
            var confirm = AnsiConsole.Prompt(new TextPrompt<string>("Confirm password:").Secret());
             if (pass != confirm) 
            {
                AnsiConsole.MarkupLine("[red]Passwords do not match.[/]");
                return;
            }

            var tempEncryptor = new PortableEncryptionService(pass);
            // Verify path
            try {
                var json = System.Text.Json.JsonSerializer.Serialize(keys, OpenerJsonContext.Default.ListOKey);
                var encrypted = tempEncryptor.Encrypt(json);
                System.IO.File.WriteAllText(path, encrypted);
                AnsiConsole.MarkupLine($"[green]Exported {keys.Count} keys to {path}[/]");
            } catch (Exception ex) {
                AnsiConsole.MarkupLine($"[red]Export failed:[/] {ex.Message}");
            }
        }, exportPathArg);
        rootCommand.AddCommand(exportCommand);

        var importCommand = new Command("import", "Import keys from a portable encrypted file");
        var importPathArg = new Argument<string>("file", "Input file path");
        importCommand.AddArgument(importPathArg);
        importCommand.SetHandler((string path) => 
        {
            if (!System.IO.File.Exists(path))
            {
                AnsiConsole.MarkupLine($"[red]File not found: {path}[/]");
                return;
            }

            var pass = AnsiConsole.Prompt(new TextPrompt<string>("Enter password for import file:").Secret());
            var tempEncryptor = new PortableEncryptionService(pass);

            try 
            {
                var content = System.IO.File.ReadAllText(path);
                var json = tempEncryptor.Decrypt(content);
                var importedKeys = System.Text.Json.JsonSerializer.Deserialize(json, OpenerJsonContext.Default.ListOKey);

                if (importedKeys != null)
                {
                    var currentKeys = storageService.GetKeys();
                    int added = 0;
                    int updated = 0;
                    foreach(var k in importedKeys)
                    {
                        var existing = currentKeys.FirstOrDefault(x => x.Key.Equals(k.Key, StringComparison.OrdinalIgnoreCase));
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

        }, importPathArg);
        rootCommand.AddCommand(importCommand);

        // --- EXISTING COMMANDS ---

        var addCommand = new Command("add", "Add a new key");
        var addKeyArg = new Argument<string>("key");
        var addValArg = new Argument<string>("value");
        var addTypeOpt = new Option<OKeyType>(new[] { "-t", "--type" }, () => OKeyType.Data, "Type of the key");
        addCommand.AddArgument(addKeyArg);
        addCommand.AddArgument(addValArg);
        addCommand.AddOption(addTypeOpt);
        addCommand.SetHandler((string k, string v, OKeyType t) =>
        {
            var keys = storageService.GetKeys();
            if (keys.Any(x => x?.Key != null && x.Key.Equals(k, StringComparison.OrdinalIgnoreCase)))
            {
                AnsiConsole.MarkupLine($"[red]Key '{k}' already exists. Use update command.[/]");
                return;
            }
            keys.Add(new OKey { Key = k ?? string.Empty, Value = v ?? string.Empty, KeyType = t });
            storageService.SaveKeys(keys);
            AnsiConsole.MarkupLine($"[green]Key '{k}' added successfully![/]");
        }, addKeyArg, addValArg, addTypeOpt);
        rootCommand.AddCommand(addCommand);

        // UPDATE
        var updateCommand = new Command("update", "Update an existing key");
        var upKeyArg = new Argument<string>("key");
        var upValArg = new Argument<string>("value");
        updateCommand.AddArgument(upKeyArg);
        updateCommand.AddArgument(upValArg);
        updateCommand.SetHandler((string k, string v) =>
        {
            var keys = storageService.GetKeys();
            var existing = keys.FirstOrDefault(x => x?.Key != null && x.Key.Equals(k, StringComparison.OrdinalIgnoreCase));
            if (existing == null) { AnsiConsole.MarkupLine($"[red]Key '{k}' not found.[/]"); return; }
            existing.Value = v ?? string.Empty;
            storageService.SaveKeys(keys);
            AnsiConsole.MarkupLine($"[green]Key '{k}' updated successfully![/]");
        }, upKeyArg, upValArg);
        rootCommand.AddCommand(updateCommand);

        // DELETE
        var deleteCommand = new Command("delete", "Delete a key");
        var delKeyArg = new Argument<string>("key");
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
        var listCommand = new Command("list", "List all keys");
        listCommand.SetHandler(() =>
        {
            var keys = storageService.GetKeys();
            if (keys == null || keys.Count == 0) { AnsiConsole.MarkupLine("[yellow]No keys found.[/]"); return; }
            var table = new Table();
            table.AddColumn("Key");
            table.AddColumn("Type");
            table.AddColumn("Value (Preview)");
            foreach (var key in keys.Where(x => x != null).OrderBy(x => x.Key))
            {
                string valPreview = (key.Value ?? string.Empty).Length > 50 ? key.Value!.Substring(0, 47) + "..." : (key.Value ?? string.Empty);
                if(key.KeyType == OKeyType.Data || key.KeyType == OKeyType.JsonData) valPreview = "********";
                table.AddRow(key.Key ?? "N/A", key.KeyType.ToString(), Markup.Escape(valPreview));
            }
            AnsiConsole.Write(table);
        });
        rootCommand.AddCommand(listCommand);

        // Implicit handler
        rootCommand.SetHandler(async (string keyResult, string[] actArgs) =>
        {
            if (string.IsNullOrEmpty(keyResult))
            {
                AnsiConsole.MarkupLine("Opener Tool - Use [bold]o --help[/] for details.");
                return; 
            }
            var keys = storageService.GetKeys();
            var foundKey = keys.FirstOrDefault(k => k?.Key != null && k.Key.Equals(keyResult, StringComparison.OrdinalIgnoreCase));
            if (foundKey == null)
            {
                AnsiConsole.MarkupLine($"[red]Key '{keyResult}' not found.[/]");
                return;
            }
            await actionService.ExecuteAsync(foundKey, actArgs);
        }, keyArgument, actArgsArgument);

        await new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .Build()
            .InvokeAsync(args);
    }
}
