namespace Opener.Models;

public class EmailTemplateData
{
    public string To { get; set; } = string.Empty;
    public string Cc { get; set; } = string.Empty;
    public string Bcc { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Provider { get; set; } = "system"; // "system", "smtp", "graph"
}
