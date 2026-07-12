using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
    public async Task HandleEmailTemplate_PascalCaseJson_StillBinds()
    {
        // Both README.md and the generated docs document PascalCase field names
        // ("To", "Subject", "Provider") for this schema. Before OpenerJsonContext was
        // made case-insensitive, that JSON silently failed to bind - fields stayed at
        // their defaults (e.g. Provider stayed "system" even when "Provider":"smtp" was
        // set), routing to the wrong provider without any error. Verified live against
        // the real binary before this fix: PascalCase "Provider":"smtp" opened the
        // system mailto client instead of attempting SMTP.
        var emailServiceMock = new Mock<IEmailService>();
        var configServiceMock = new Mock<IConfigService>();
        configServiceMock.Setup(c => c.GetConfig()).Returns(new OpenerConfig());

        var service = new ActionService(configService: configServiceMock.Object, emailService: emailServiceMock.Object);
        var key = new OKey
        {
            Key = "test-email",
            KeyType = OKeyType.EmailTemplate,
            Value = "{\"To\":\"a@b.com\",\"Subject\":\"Hi\",\"Body\":\"test\",\"Provider\":\"smtp\"}"
        };

        await service.ExecuteAsync(key, new string[0]);

        emailServiceMock.Verify(e => e.SendEmailAsync(It.Is<EmailTemplateData>(t =>
            t.To == "a@b.com" && t.Subject == "Hi" && t.Body == "test" && t.Provider == "smtp"
        )), Times.Once);
    }

    [Fact]
    public async Task HandleCalendarEvent_PascalCaseJson_StillBinds()
    {
        var calendarServiceMock = new Mock<ICalendarService>();
        var configServiceMock = new Mock<IConfigService>();
        configServiceMock.Setup(c => c.GetConfig()).Returns(new OpenerConfig());

        var service = new ActionService(configService: configServiceMock.Object, calendarService: calendarServiceMock.Object);
        var key = new OKey
        {
            Key = "test-event",
            KeyType = OKeyType.CalendarEvent,
            Value = "{\"Subject\":\"Sync\",\"Body\":\"Roadmap\",\"Invitees\":\"a@b.com\",\"DurationMinutes\":30,\"Provider\":\"graph\",\"StartTime\":\"tomorrow 10:00\"}"
        };

        await service.ExecuteAsync(key, new string[0]);

        calendarServiceMock.Verify(c => c.CreateEventAsync(It.Is<CalendarEventData>(e =>
            e.Subject == "Sync" && e.Body == "Roadmap" && e.Invitees == "a@b.com" &&
            e.DurationMinutes == 30 && e.Provider == "graph"
        )), Times.Once);
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

    // ---------- HandleRest (single-step and chained) ----------

    [Fact]
    public async Task HandleRest_SingleStep_CamelCaseJson_ResolvesUrlAndSendsHeaders()
    {
        var senderMock = new Mock<IHttpRequestSender>();
        HttpRequestMessage? captured = null;
        senderMock.Setup(s => s.SendAsync(It.IsAny<HttpRequestMessage>()))
            .Callback<HttpRequestMessage>(r => captured = r)
            .ReturnsAsync(new HttpCallResult(200, true, "{\"ok\":true}"));

        var service = new ActionService(httpRequestSender: senderMock.Object);
        var key = new OKey
        {
            Key = "api",
            KeyType = OKeyType.Rest,
            Value = "{\"url\":\"https://api.example.com/users/{0}\",\"method\":\"GET\",\"headers\":{\"User-Agent\":\"Opener-CLI\"}}"
        };

        await service.ExecuteAsync(key, new[] { "42" });

        Assert.NotNull(captured);
        Assert.Equal("https://api.example.com/users/42", captured!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, captured.Method);
        Assert.True(captured.Headers.TryGetValues("User-Agent", out var values));
        Assert.Equal("Opener-CLI", values!.First());
    }

    [Fact]
    public async Task HandleRest_SingleStep_LegacyPascalCaseJson_StillWorks()
    {
        // Before the casing fix, RestData only matched exact PascalCase property names.
        // Case-insensitive matching must keep that working alongside the now-documented
        // camelCase shape.
        var senderMock = new Mock<IHttpRequestSender>();
        HttpRequestMessage? captured = null;
        senderMock.Setup(s => s.SendAsync(It.IsAny<HttpRequestMessage>()))
            .Callback<HttpRequestMessage>(r => captured = r)
            .ReturnsAsync(new HttpCallResult(200, true, "{}"));

        var service = new ActionService(httpRequestSender: senderMock.Object);
        var key = new OKey
        {
            Key = "api",
            KeyType = OKeyType.Rest,
            Value = "{\"Url\":\"https://api.example.com/ping\",\"Method\":\"POST\"}"
        };

        await service.ExecuteAsync(key, new string[0]);

        Assert.NotNull(captured);
        Assert.Equal("https://api.example.com/ping", captured!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, captured.Method);
    }

    [Fact]
    public async Task HandleRest_HeaderContentType_OverridesDefaultBodyMediaType()
    {
        var senderMock = new Mock<IHttpRequestSender>();
        HttpRequestMessage? captured = null;
        senderMock.Setup(s => s.SendAsync(It.IsAny<HttpRequestMessage>()))
            .Callback<HttpRequestMessage>(r => captured = r)
            .ReturnsAsync(new HttpCallResult(200, true, "ok"));

        var service = new ActionService(httpRequestSender: senderMock.Object);
        var key = new OKey
        {
            Key = "api",
            KeyType = OKeyType.Rest,
            Value = "{\"url\":\"https://api.example.com/submit\",\"method\":\"POST\",\"body\":\"a=1&b=2\",\"headers\":{\"Content-Type\":\"application/x-www-form-urlencoded\"}}"
        };

        await service.ExecuteAsync(key, new string[0]);

        Assert.NotNull(captured?.Content);
        Assert.Equal("application/x-www-form-urlencoded", captured!.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task HandleRest_Chain_ExtractsTokenAndSubstitutesIntoNextStepHeader()
    {
        var senderMock = new Mock<IHttpRequestSender>();
        var capturedRequests = new List<HttpRequestMessage>();
        var responses = new Queue<HttpCallResult>(new[]
        {
            new HttpCallResult(200, true, "{\"access_token\":\"secret-token-123\"}"),
            new HttpCallResult(200, true, "{\"data\":\"final-result\"}")
        });
        senderMock.Setup(s => s.SendAsync(It.IsAny<HttpRequestMessage>()))
            .Returns<HttpRequestMessage>(req =>
            {
                capturedRequests.Add(req);
                return Task.FromResult(responses.Dequeue());
            });

        var service = new ActionService(httpRequestSender: senderMock.Object);
        var chainJson = "{\"steps\":[" +
            "{\"url\":\"https://api.example.com/login\",\"method\":\"POST\",\"extract\":{\"token\":\"access_token\"}}," +
            "{\"url\":\"https://api.example.com/data\",\"method\":\"GET\",\"headers\":{\"Authorization\":\"Bearer {{token}}\"}}" +
            "]}";
        var key = new OKey { Key = "chain", KeyType = OKeyType.Rest, Value = chainJson };

        await service.ExecuteAsync(key, new string[0]);

        Assert.Equal(2, capturedRequests.Count);
        Assert.True(capturedRequests[1].Headers.TryGetValues("Authorization", out var authValues));
        Assert.Equal("Bearer secret-token-123", authValues!.First());
    }

    [Fact]
    public async Task HandleRest_Chain_AbortsWhenNonFinalStepFails()
    {
        var senderMock = new Mock<IHttpRequestSender>();
        senderMock.Setup(s => s.SendAsync(It.IsAny<HttpRequestMessage>()))
            .ReturnsAsync(new HttpCallResult(401, false, "{\"error\":\"unauthorized\"}"));

        var service = new ActionService(httpRequestSender: senderMock.Object);
        var chainJson = "{\"steps\":[" +
            "{\"url\":\"https://api.example.com/login\",\"method\":\"POST\"}," +
            "{\"url\":\"https://api.example.com/data\",\"method\":\"GET\"}" +
            "]}";
        var key = new OKey { Key = "chain", KeyType = OKeyType.Rest, Value = chainJson };

        await service.ExecuteAsync(key, new string[0]);

        senderMock.Verify(s => s.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    [Fact]
    public async Task HandleRest_Chain_ExtractionNotFound_LeavesLiteralPlaceholderInNextStep()
    {
        // Extraction failures warn but don't abort - the unresolved {{token}} placeholder
        // should pass through literally to the next step rather than silently vanishing.
        var senderMock = new Mock<IHttpRequestSender>();
        var capturedRequests = new List<HttpRequestMessage>();
        var responses = new Queue<HttpCallResult>(new[]
        {
            new HttpCallResult(200, true, "{\"unexpected\":\"shape\"}"),
            new HttpCallResult(200, true, "{}")
        });
        senderMock.Setup(s => s.SendAsync(It.IsAny<HttpRequestMessage>()))
            .Returns<HttpRequestMessage>(req =>
            {
                capturedRequests.Add(req);
                return Task.FromResult(responses.Dequeue());
            });

        var service = new ActionService(httpRequestSender: senderMock.Object);
        var chainJson = "{\"steps\":[" +
            "{\"url\":\"https://api.example.com/login\",\"extract\":{\"token\":\"access_token\"}}," +
            "{\"url\":\"https://api.example.com/data\",\"headers\":{\"Authorization\":\"Bearer {{token}}\"}}" +
            "]}";
        var key = new OKey { Key = "chain", KeyType = OKeyType.Rest, Value = chainJson };

        await service.ExecuteAsync(key, new string[0]);

        Assert.Equal(2, capturedRequests.Count);
        Assert.True(capturedRequests[1].Headers.TryGetValues("Authorization", out var authValues));
        Assert.Equal("Bearer {{token}}", authValues!.First());
    }
}
