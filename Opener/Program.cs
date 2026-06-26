using System;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using Opener.Commands;
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
        var graphAuthService = new GraphAuthService(storageService);
        var emailService = new EmailService(storageService, graphAuthService);
        var calendarService = new CalendarService(storageService, graphAuthService);
        var actionService = new ActionService(configService, storageService, emailService, calendarService);

        // 1.5 Auto-initialize storage
        try 
        {
            storageService.Initialize();
        }
        catch (Exception ex)
        {
            // Log the initialization warning but don't crash
            // This allows users to run 'config set-location' to fix a broken storage path
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Storage initialization failed: {ex.Message}");
            AnsiConsole.MarkupLine("[yellow]Use 'opener config set-location <path>' to change storage location if needed.[/]");
        }

        // 2. Build Root Command and Run
        var rootCommand = new OpenerRootCommand(
            configService,
            storageService,
            actionService,
            credentialService,
            graphAuthService
        );

        await new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .Build()
            .InvokeAsync(args);
    }
}
