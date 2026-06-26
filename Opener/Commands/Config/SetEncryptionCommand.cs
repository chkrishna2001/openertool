using System;
using System.CommandLine;
using System.IO;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class SetEncryptionCommand : Command
{
    public SetEncryptionCommand(IConfigService configService, ICredentialService credentialService, IStorageService storageService, IAnsiConsole? console = null)
        : base("set-encryption", "Switch between local machine encryption and portable password encryption.\n\n" +
                                 "Examples:\n" +
                                 "  o config set-encryption local\n" +
                                 "  o config set-encryption portable --password my-secret")
    {
        var _console = console ?? AnsiConsole.Console;

        var modeArg = new Argument<string>("mode", "Encryption mode: local or portable.");
        var setEncPassOpt = new Option<string>(new[] { "-p", "--password" }, "Portable-mode password. Omit it to be prompted interactively.");
        
        AddArgument(modeArg);
        AddOption(setEncPassOpt);

        this.SetHandler((string mode, string? passInput) => 
        {
            mode = mode.ToLower();
            if (mode != "local" && mode != "portable") 
            {
                _console.MarkupLine("[red]Invalid mode. Use 'local' or 'portable'.[/]");
                return;
            }

            var conf = configService.GetConfig();
            if (conf.EncryptionMode == mode)
            {
                _console.MarkupLine($"[yellow]Already in {mode} mode.[/]");
                return;
            }

            // AUTO-BACKUP BEFORE MIGRATION
            try
            {
                var backupPath = Path.Combine(Path.GetDirectoryName(configService.GetDataFilePath()) ?? "." , ".backup");
                Directory.CreateDirectory(backupPath);
                var backupFile = Path.Combine(backupPath, $"opener_backup_{DateTime.Now:yyyyMMdd_HHmmss}.dat");
                if (File.Exists(configService.GetDataFilePath()))
                {
                    File.Copy(configService.GetDataFilePath(), backupFile, true);
                    _console.MarkupLine($"[dim]Auto-backup created: {backupFile}[/]");
                }
            }
            catch (Exception backupEx)
            {
                _console.MarkupLine($"[yellow]Warning: Could not create backup: {backupEx.Message}[/]");
            }
            
            if (!_console.Confirm($"Switch from {(configService.IsPortableMode() ? "portable" : "local")} to {mode} encryption mode? Keys will be re-encrypted.", false))
            {
                _console.MarkupLine("[yellow]Cancelled.[/]");
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
                    pass = _console.Prompt(new TextPrompt<string>("Set Portable Password:").Secret());
                    var confirm = _console.Prompt(new TextPrompt<string>("Confirm Password:").Secret());
                    if (pass != confirm) 
                    {
                        _console.MarkupLine("[red]Passwords do not match.[/]");
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
                _console.MarkupLine($"[green]Switched to {mode} mode and re-encrypted {currentKeys.Count} keys.[/]");
            }
            catch (Exception ex)
            {
                _console.MarkupLine($"[red]Migration failed:[/] {ex.Message}");
            }
        }, modeArg, setEncPassOpt);
    }
}
