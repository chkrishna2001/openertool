using System.CommandLine;

namespace Opener.Commands.Config;

public class ConfigCommand : Command
{
    public ConfigCommand()
        : base("config", "Manage storage, encryption, and global URL template settings.\n\n" +
                         "Examples:\n" +
                         "  o config show\n" +
                         "  o config set-encryption portable --password my-secret\n" +
                         "  o config set-url-aliases env d=-dev u=-uat p=\n" +
                         "  o config set-url-aliases --file aliases.json\n" +
                         "  o config set-default-params user kchirravuri")
    {
    }
}
