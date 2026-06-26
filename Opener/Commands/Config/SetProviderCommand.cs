using System.CommandLine;

namespace Opener.Commands.Config;

public class SetProviderCommand : Command
{
    public SetProviderCommand()
        : base("set-provider", "Configure credentials/settings for email and calendar providers.")
    {
    }
}
