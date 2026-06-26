using System;
using System.CommandLine;
using System.Threading.Tasks;
using Opener.Models;
using Opener.Services;
using Spectre.Console;

namespace Opener.Commands.Config;

public class GraphCommand : Command
{
    public GraphCommand(IStorageService storageService, IGraphAuthService graphAuthService, IAnsiConsole? console = null)
        : base("graph", "Configure Microsoft Graph API Client Credentials (daemon auth).")
    {
        var _console = console ?? AnsiConsole.Console;

        var graphTenantOpt = new Option<string>("--tenant-id", "Azure Active Directory Tenant ID") { IsRequired = true };
        var graphClientIdOpt = new Option<string>("--client-id", "Azure Active Directory Client ID") { IsRequired = true };
        var graphClientSecretOpt = new Option<string>("--client-secret", "Azure Active Directory Client Secret") { IsRequired = true };

        AddOption(graphTenantOpt);
        AddOption(graphClientIdOpt);
        AddOption(graphClientSecretOpt);

        this.SetHandler(async (string tenantId, string clientId, string clientSecret) =>
        {
            _console.MarkupLine("[yellow]Validating credentials with Microsoft Graph...[/]");
            bool isValid = await graphAuthService.ValidateClientCredentialsAsync(tenantId, clientId, clientSecret);
            if (!isValid)
            {
                _console.MarkupLine("[red]Error: Authentication failed. Credentials not saved.[/]");
                return;
            }

            var keys = storageService.GetKeys();

            void SetKey(string k, string v)
            {
                var existing = keys.Find(x => x.Key == k);
                if (existing != null) existing.Value = v;
                else keys.Add(new OKey { Key = k, Value = v, KeyType = OKeyType.Data, Description = "System Credential" });
            }

            SetKey("__provider_graph_tenant_id", tenantId);
            SetKey("__provider_graph_client_id", clientId);
            SetKey("__provider_graph_client_secret", clientSecret);

            // Clean up any Device Code flow refresh tokens to avoid conflicts
            var deviceToken = keys.Find(x => x.Key == "__provider_graph_refresh_token");
            if (deviceToken != null) keys.Remove(deviceToken);

            storageService.SaveKeys(keys);
            _console.MarkupLine("[green]✔ Microsoft Graph API Client Credentials configured and validated successfully![/]");
        }, graphTenantOpt, graphClientIdOpt, graphClientSecretOpt);
    }
}
