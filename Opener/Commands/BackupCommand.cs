using System;
using System.CommandLine;
using System.IO;
using System.Text.Json;
using Opener.Models;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands;

public class BackupCommand : Command
{
    public BackupCommand(IConfigService configService, IStorageService storageService, IAnsiConsole? console = null)
        : base("backup", "Create an encrypted backup of all keys in the .backup folder.\n\n" +
                        "Backup files are automatically stored next to your data file with timestamps.\n\n" +
                        "Example:\n" +
                        "  o backup\n" +
                        "  o backup --password my-backup-password")
    {
        var _console = console ?? AnsiConsole.Console;

        var backupPassOpt = new Option<string>(new[] { "-p", "--password" }, "Optional password. If omitted, uses current encryption key.");
        AddOption(backupPassOpt);

        this.SetHandler((string? passInput) =>
        {
            var keys = storageService.GetKeys();
            if (keys.Count == 0)
            {
                _console.MarkupLine("[yellow]No keys to backup.[/]");
                return;
            }

            try
            {
                var backupDir = Path.Combine(Path.GetDirectoryName(configService.GetDataFilePath()) ?? ".", ".backup");
                Directory.CreateDirectory(backupDir);
                
                var backupFile = Path.Combine(backupDir, $"opener_backup_{DateTime.Now:yyyyMMdd_HHmmss}.dat");
                
                // If password provided, use portable encryption. Otherwise just copy the encrypted file.
                if (!string.IsNullOrEmpty(passInput))
                {
                    var tempEncryptor = new PortableEncryptionService(passInput);
                    var json = JsonSerializer.Serialize(keys, OpenerJsonContext.Default.ListOKey);
                    var encrypted = tempEncryptor.Encrypt(json);
                    File.WriteAllText(backupFile, encrypted);
                }
                else
                {
                    // Copy current encrypted file
                    if (File.Exists(configService.GetDataFilePath()))
                    {
                        File.Copy(configService.GetDataFilePath(), backupFile, true);
                    }
                }
                
                _console.MarkupLine($"[green]✓ Backup created:[/] {backupFile}");
            }
            catch (Exception ex)
            {
                _console.MarkupLine($"[red]Backup failed:[/] {ex.Message}");
            }
        }, backupPassOpt);
    }
}
