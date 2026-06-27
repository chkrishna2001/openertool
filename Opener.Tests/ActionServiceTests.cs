using System.Text.Json;
using System.Threading.Tasks;
using Moq;
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
}
