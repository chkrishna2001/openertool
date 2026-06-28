using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Moq;
using Opener.Commands;
using Opener.Models;
using Opener.Services;
using Xunit;

namespace Opener.Tests;

public class ActionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WithUnknownType_DoesNotThrow()
    {
        var service = new ActionService();
        var key = new OKey
        {
            Key = "test",
            KeyType = (OKeyType)999,
            Value = "value"
        };

        await service.ExecuteAsync(key, new string[0]);
    }

    [Fact]
    public async Task HandleEmailTemplate_ResolvesAttachmentPath()
    {
        var emailServiceMock = new Mock<IEmailService>();
        var configServiceMock = new Mock<IConfigService>();
        
        configServiceMock.Setup(c => c.GetConfig()).Returns(new OpenerConfig());

        var service = new ActionService(
            configService: configServiceMock.Object,
            emailService: emailServiceMock.Object
        );

        var key = new OKey
        {
            Key = "test-email",
            KeyType = OKeyType.EmailTemplate,
            Value = JsonSerializer.Serialize(new EmailTemplateData
            {
                To = "test@example.com",
                Cc = "cc@example.com",
                Bcc = "bcc@example.com",
                Subject = "Hello {0}",
                Body = "Body {1}",
                AttachmentPath = "C:\\files\\{0}_report.pdf",
                Provider = "smtp"
            }, OpenerJsonContext.Default.EmailTemplateData)
        };

        await service.ExecuteAsync(key, new[] { "June", "content" });

        emailServiceMock.Verify(e => e.SendEmailAsync(It.Is<EmailTemplateData>(t =>
            t.To == "test@example.com" &&
            t.Cc == "cc@example.com" &&
            t.Bcc == "bcc@example.com" &&
            t.Subject == "Hello June" &&
            t.Body == "Body content" &&
            t.AttachmentPath == "C:\\files\\June_report.pdf" &&
            t.Provider == "smtp"
        )), Times.Once);
    }

    [Fact]
    public void NormalizeJson_WithSingleQuotes_NormalizesCorrectly()
    {
        var input = "{ 'to': 'chkrishna2001@yahoo.com', 'subject': 'see if this returns date - {0}', 'body': 'hi this\\'s test email', 'attachmentPath': 'tet-attachment-{1}.txt', 'provider':'smtp'}";
        var expected = "{\"to\":\"chkrishna2001@yahoo.com\",\"subject\":\"see if this returns date - {0}\",\"body\":\"hi this's test email\",\"attachmentPath\":\"tet-attachment-{1}.txt\",\"provider\":\"smtp\"}";
        
        var normalized = CommandHelpers.NormalizeJson(input);
        
        // Remove spaces for comparison if any, or just compare exact expected string
        // Let's assert exact mapping:
        var normalizedWithoutWhitespace = string.Concat(normalized.Where(c => !char.IsWhiteSpace(c)));
        var expectedWithoutWhitespace = string.Concat(expected.Where(c => !char.IsWhiteSpace(c)));
        
        Assert.Equal(expectedWithoutWhitespace, normalizedWithoutWhitespace);

        // Deserializing should succeed
        var data = JsonSerializer.Deserialize(normalized, OpenerJsonContext.Default.EmailTemplateData);
        Assert.NotNull(data);
        Assert.Equal("chkrishna2001@yahoo.com", data.To);
        Assert.Equal("see if this returns date - {0}", data.Subject);
        Assert.Equal("hi this's test email", data.Body);
        Assert.Equal("tet-attachment-{1}.txt", data.AttachmentPath);
        Assert.Equal("smtp", data.Provider);
    }
}
