using System;
using System.CommandLine;
using System.Text.Json;
using Opener.Models;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class ShowCommand : Command
{
    public ShowCommand(IConfigService configService, IAnsiConsole? console = null)
        : base("show", "Show current storage path, encryption mode, global URL aliases, and global default params.")
    {
        var _console = console ?? AnsiConsole.Console;

        this.SetHandler(() => 
        {
            var conf = configService.GetConfig();
            _console.MarkupLine($"[bold]Storage Location:[/] {configService.GetDataFilePath()}");
            _console.MarkupLine($"[bold]Encryption Mode:[/] {conf.EncryptionMode}");
            _console.MarkupLine("[bold]Global URL Aliases:[/]");
            _console.WriteLine(JsonSerializer.Serialize(conf.GlobalUrlAliases, OpenerJsonContext.Default.DictionaryStringDictionaryStringString));
            _console.MarkupLine("[bold]Global Default Params:[/]");
            _console.WriteLine(JsonSerializer.Serialize(conf.GlobalDefaultParams, OpenerJsonContext.Default.DictionaryStringString));
            if (conf.EncryptionMode == "portable")
            {
                _console.MarkupLine("[dim](Password is cached in Windows Credential Manager)[/]");
            }
        });
    }
}
