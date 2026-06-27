using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Opener.Models;
using Spectre.Console;

namespace Opener.Services;

public class GraphEmailAddress
{
    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;
}

public class GraphRecipient
{
    [JsonPropertyName("emailAddress")]
    public GraphEmailAddress EmailAddress { get; set; } = new();
}

public class GraphAttachment
{
    [JsonPropertyName("@odata.type")]
    public string ODataType { get; set; } = "#microsoft.graph.fileAttachment";

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "application/octet-stream";

    [JsonPropertyName("contentBytes")]
    public string ContentBytes { get; set; } = string.Empty;
}

public class GraphItemBody
{
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "Text";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class GraphMessage
{
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public GraphItemBody Body { get; set; } = new();

    [JsonPropertyName("toRecipients")]
    public List<GraphRecipient> ToRecipients { get; set; } = new();

    [JsonPropertyName("ccRecipients")]
    public List<GraphRecipient> CcRecipients { get; set; } = new();

    [JsonPropertyName("bccRecipients")]
    public List<GraphRecipient> BccRecipients { get; set; } = new();

    [JsonPropertyName("attachments")]
    public List<GraphAttachment>? Attachments { get; set; }
}

public class GraphSendMailRequest
{
    [JsonPropertyName("message")]
    public GraphMessage Message { get; set; } = new();

    [JsonPropertyName("saveToSentItems")]
    public string SaveToSentItems { get; set; } = "true";
}

[JsonSerializable(typeof(GraphSendMailRequest))]
[JsonSerializable(typeof(GraphMessage))]
[JsonSerializable(typeof(GraphAttachment))]
internal partial class GraphSendMailJsonContext : JsonSerializerContext { }

public interface IEmailService
{
    Task SendEmailAsync(EmailTemplateData emailTemplate);
}

public class EmailService : IEmailService
{
    private readonly IStorageService _storageService;
    private readonly IGraphAuthService _graphAuthService;

    public EmailService(IStorageService storageService, IGraphAuthService graphAuthService)
    {
        _storageService = storageService;
        _graphAuthService = graphAuthService;
    }

    public async Task SendEmailAsync(EmailTemplateData emailTemplate)
    {
        var provider = emailTemplate.Provider?.ToLowerInvariant() ?? "system";

        switch (provider)
        {
            case "system":
                SendSystemEmail(emailTemplate);
                break;
            case "smtp":
                await SendSmtpEmailAsync(emailTemplate);
                break;
            case "graph":
                await SendGraphEmailAsync(emailTemplate);
                break;
            default:
                AnsiConsole.MarkupLine($"[red]Unknown email provider '{provider}'. Defaulting to 'system' mailto.[/]");
                SendSystemEmail(emailTemplate);
                break;
        }
    }

    private void SendSystemEmail(EmailTemplateData emailTemplate)
    {
        AnsiConsole.MarkupLine("[yellow]Opening default email client (system mailto)...[/]");

        if (!string.IsNullOrEmpty(emailTemplate.AttachmentPath))
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Attachments are not reliably supported by the mailto protocol and may need to be attached manually.");
            AnsiConsole.MarkupLine($"[blue]Attachment:[/] {emailTemplate.AttachmentPath}");
        }
        
        var toEscaped = Uri.EscapeDataString(emailTemplate.To);
        var ccEscaped = Uri.EscapeDataString(emailTemplate.Cc);
        var bccEscaped = Uri.EscapeDataString(emailTemplate.Bcc);
        var subjectEscaped = Uri.EscapeDataString(emailTemplate.Subject);
        var bodyEscaped = Uri.EscapeDataString(emailTemplate.Body);

        var mailtoUrl = $"mailto:{toEscaped}?cc={ccEscaped}&bcc={bccEscaped}&subject={subjectEscaped}&body={bodyEscaped}";
        
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(mailtoUrl) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", mailtoUrl);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", mailtoUrl);
            }
            AnsiConsole.MarkupLine("[green]✔ Default email client launched successfully.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error launching default mail client:[/] {ex.Message}");
            AnsiConsole.MarkupLine($"Resolved URL was: {mailtoUrl}");
        }
    }

    private async Task SendSmtpEmailAsync(EmailTemplateData emailTemplate)
    {
        AnsiConsole.MarkupLine("[yellow]Preparing email to send via SMTP...[/]");

        var keys = _storageService.GetKeys();
        var server = keys.Find(k => k.Key == "__provider_smtp_server")?.Value;
        var portStr = keys.Find(k => k.Key == "__provider_smtp_port")?.Value;
        var sslStr = keys.Find(k => k.Key == "__provider_smtp_ssl")?.Value;
        var username = keys.Find(k => k.Key == "__provider_smtp_username")?.Value;
        var password = keys.Find(k => k.Key == "__provider_smtp_password")?.Value;

        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(portStr) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            throw new Exception("SMTP provider is not fully configured. Configure it with 'o config set-provider smtp'.");
        }

        if (!int.TryParse(portStr, out int port))
        {
            throw new Exception($"Invalid SMTP port: {portStr}");
        }

        bool ssl = bool.TryParse(sslStr, out bool s) && s;

        try
        {
            using var smtp = new SmtpClient(server, port)
            {
                EnableSsl = ssl,
                Credentials = new NetworkCredential(username, password),
                Timeout = 15000 // 15 seconds timeout
            };

            using var mail = new MailMessage();
            mail.From = new MailAddress(username);

            var toList = ParseEmailList(emailTemplate.To);
            var ccList = ParseEmailList(emailTemplate.Cc);
            var bccList = ParseEmailList(emailTemplate.Bcc);

            if (!toList.Any())
            {
                throw new Exception("Email template must specify at least one recipient in the 'To' field.");
            }

            foreach (var to in toList) mail.To.Add(new MailAddress(to));
            foreach (var cc in ccList) mail.CC.Add(new MailAddress(cc));
            foreach (var bcc in bccList) mail.Bcc.Add(new MailAddress(bcc));

            mail.Subject = emailTemplate.Subject;
            mail.Body = emailTemplate.Body;

            if (!string.IsNullOrEmpty(emailTemplate.AttachmentPath))
            {
                var fullPath = Path.GetFullPath(emailTemplate.AttachmentPath);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Attachment file not found: {fullPath}");
                }
                mail.Attachments.Add(new Attachment(fullPath));
            }

            AnsiConsole.MarkupLine($"[yellow]Connecting to SMTP server {server}:{port}...[/]");
            await smtp.SendMailAsync(mail);
            AnsiConsole.MarkupLine($"[green]✔ Email successfully sent to {emailTemplate.To} via SMTP![/]");
        }
        catch (Exception ex)
        {
            throw new Exception($"SMTP Email Sending failed: {ex.Message}", ex);
        }
    }

    private async Task SendGraphEmailAsync(EmailTemplateData emailTemplate)
    {
        AnsiConsole.MarkupLine("[yellow]Acquiring Microsoft Graph access token...[/]");
        var token = await _graphAuthService.GetAccessTokenAsync();

        if (string.IsNullOrEmpty(token))
        {
            throw new Exception("Not authenticated with Microsoft Graph. Run 'o config auth-graph' or set client credentials via 'o config set-provider graph'.");
        }

        AnsiConsole.MarkupLine("[yellow]Sending email via Microsoft Graph API...[/]");

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var toList = ParseEmailList(emailTemplate.To);
        var ccList = ParseEmailList(emailTemplate.Cc);
        var bccList = ParseEmailList(emailTemplate.Bcc);

        if (!toList.Any())
        {
            throw new Exception("Email template must specify at least one recipient in the 'To' field.");
        }

        List<GraphAttachment>? graphAttachments = null;
        if (!string.IsNullOrEmpty(emailTemplate.AttachmentPath))
        {
            var fullPath = Path.GetFullPath(emailTemplate.AttachmentPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Attachment file not found: {fullPath}");
            }

            var fileName = Path.GetFileName(fullPath);
            var contentBytes = Convert.ToBase64String(await File.ReadAllBytesAsync(fullPath));
            var contentType = GetMimeType(fileName);

            graphAttachments = new List<GraphAttachment>
            {
                new()
                {
                    Name = fileName,
                    ContentType = contentType,
                    ContentBytes = contentBytes
                }
            };
        }

        var payload = new GraphSendMailRequest
        {
            Message = new GraphMessage
            {
                Subject = emailTemplate.Subject,
                Body = new GraphItemBody
                {
                    ContentType = "Text",
                    Content = emailTemplate.Body
                },
                ToRecipients = toList.Select(e => new GraphRecipient { EmailAddress = new GraphEmailAddress { Address = e } }).ToList(),
                CcRecipients = ccList.Select(e => new GraphRecipient { EmailAddress = new GraphEmailAddress { Address = e } }).ToList(),
                BccRecipients = bccList.Select(e => new GraphRecipient { EmailAddress = new GraphEmailAddress { Address = e } }).ToList(),
                Attachments = graphAttachments
            },
            SaveToSentItems = "true"
        };

        var jsonPayload = JsonSerializer.Serialize(payload, GraphSendMailJsonContext.Default.GraphSendMailRequest);
        var httpContent = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

        // MS Graph `/me/sendMail` is used. If this is a daemon app (client credentials), /me doesn't work.
        // We look for tenant id to see if client credentials were used, if so we should check for a registered sender or fallback to /users/{sender}/sendMail.
        var keys = _storageService.GetKeys();
        var clientSecret = keys.Find(k => k.Key == "__provider_graph_client_secret")?.Value;
        
        string endpoint = "https://graph.microsoft.com/v1.0/me/sendMail";
        
        if (!string.IsNullOrEmpty(clientSecret))
        {
            // For client credentials flow, /me is not available since there is no logged-in user.
            // We use the SMTP/graph configuration username as the sender or user email.
            var senderEmail = keys.Find(k => k.Key == "__provider_smtp_username")?.Value;
            if (string.IsNullOrEmpty(senderEmail))
            {
                // Fallback: try to see if username is configured elsewhere, or prompt
                throw new Exception("Client credentials flow is active. Please define the sender email by configuring a username via 'o config set-provider smtp --username sender@company.com ...' or as fallback.");
            }
            endpoint = $"https://graph.microsoft.com/v1.0/users/{senderEmail}/sendMail";
        }

        var response = await client.PostAsync(endpoint, httpContent);

        if (response.IsSuccessStatusCode)
        {
            AnsiConsole.MarkupLine($"[green]✔ Email successfully sent to {emailTemplate.To} via Microsoft Graph API![/]");
        }
        else
        {
            var errContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Microsoft Graph API failed (Status: {response.StatusCode}): {errContent}");
        }
    }

    private static List<string> ParseEmailList(string emails)
    {
        if (string.IsNullOrWhiteSpace(emails)) return new List<string>();
        return emails.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(e => e.Trim())
                     .Where(e => !string.IsNullOrEmpty(e))
                     .ToList();
    }

    private static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".html" => "text/html",
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
    }
}
