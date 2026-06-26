using System;
using System.CommandLine;
using System.Threading.Tasks;
using Opener.Services;

namespace Opener.Commands.Config;

public class AuthGraphCommand : Command
{
    public AuthGraphCommand(IGraphAuthService graphAuthService)
        : base("auth-graph", "Authenticate Microsoft Graph via interactive Device Code Flow.")
    {
        var authClientIdOpt = new Option<string?>("--client-id", "Optional custom Azure AD Client ID");
        AddOption(authClientIdOpt);

        this.SetHandler(async (string? customClientId) =>
        {
            await graphAuthService.StartDeviceCodeAuthAsync(customClientId);
        }, authClientIdOpt);
    }
}
