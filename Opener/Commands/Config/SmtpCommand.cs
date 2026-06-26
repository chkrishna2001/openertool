using System;
using System.CommandLine;
using Opener.Models;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class SmtpCommand : Command
{
    public SmtpCommand(IStorageService storageService, IAnsiConsole? console = null)
        : base("smtp", "Configure SMTP credentials.")
    {
        var _console = console ?? AnsiConsole.Console;

        var smtpServerOpt = new Option<string>("--server", "SMTP server host (e.g., smtp.gmail.com)") { IsRequired = true };
        var smtpPortOpt = new Option<int>("--port", "SMTP server port (e.g., 587)") { IsRequired = true };
        var smtpSslOpt = new Option<bool>("--ssl", "Enable SSL/TLS");
        var smtpUserOpt = new Option<string>("--username", "SMTP username/email") { IsRequired = true };
        var smtpPassOpt = new Option<string>("--password", "SMTP password or App Password") { IsRequired = true };
        
        AddOption(smtpServerOpt);
        AddOption(smtpPortOpt);
        AddOption(smtpSslOpt);
        AddOption(smtpUserOpt);
        AddOption(smtpPassOpt);

        this.SetHandler((string server, int port, bool ssl, string username, string password) =>
        {
            var keys = storageService.GetKeys();
            
            void SetKey(string k, string v)
            {
                var existing = keys.Find(x => x.Key == k);
                if (existing != null) existing.Value = v;
                else keys.Add(new OKey { Key = k, Value = v, KeyType = OKeyType.Data, Description = "System Credential" });
            }

            SetKey("__provider_smtp_server", server);
            SetKey("__provider_smtp_port", port.ToString());
            SetKey("__provider_smtp_ssl", ssl.ToString());
            SetKey("__provider_smtp_username", username);
            SetKey("__provider_smtp_password", password);

            storageService.SaveKeys(keys);
            _console.MarkupLine("[green]✔ SMTP provider configured successfully![/]");
        }, smtpServerOpt, smtpPortOpt, smtpSslOpt, smtpUserOpt, smtpPassOpt);
    }
}
