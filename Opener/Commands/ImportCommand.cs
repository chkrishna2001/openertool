using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.Json;
using Opener.Models;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands;

public class ImportCommand : Command
{
    public ImportCommand(IStorageService storageService, IAnsiConsole? console = null, IConfigService? configService = null, IGitSyncService? gitSyncService = null)
        : base("import", "Import keys from a portable encrypted backup file. Existing keys with the same name are updated.\n\n" +
                        "Example:\n" +
                        "  o import backup.dat --password my-export-password")
    {
        var _console = console ?? AnsiConsole.Console;

        var importPathArg = new Argument<string>("file", "Input encrypted backup file path.");
        var importPassOpt = new Option<string>(new[] { "-p", "--password" }, "Password for the import file. Omit it to be prompted interactively.");
        
        AddArgument(importPathArg);
        AddOption(importPassOpt);

        this.SetHandler((string path, string? passInput) => 
        {
            if (!File.Exists(path))
            {
                _console.MarkupLine($"[red]File not found: {path}[/]");
                return;
            }

            string pass = passInput ?? string.Empty;
            if (string.IsNullOrEmpty(pass))
            {
                pass = _console.Prompt(new TextPrompt<string>("Enter password for import file:").Secret());
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
                    _console.MarkupLine($"[green]Import successful. Added: {added}, Updated: {updated}[/]");
                    AutoSyncCoordinator.TriggerIfEnabled(configService, gitSyncService);
                }
            }
            catch (Exception ex)
            {
                 _console.MarkupLine($"[red]Import failed (Wrong password?):[/] {ex.Message}");
            }
        }, importPathArg, importPassOpt);
    }
}
