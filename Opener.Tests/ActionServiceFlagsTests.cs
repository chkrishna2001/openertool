using System;
using System.IO;
using System.Threading.Tasks;
using Opener.Models;
using Opener.Services;
using Xunit;

namespace Opener.Tests;

public class ActionServiceFlagsTests
{
    [Fact]
    public async Task WebPath_ReturnsResolvedUrl_WhenReturnFlagTrue()
    {
        var service = new ActionService();
        var key = new OKey
        {
            Key = "testweb",
            KeyType = OKeyType.WebPath,
            Value = "https://example.com/{0}"
        };

        var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            await service.ExecuteAsync(key, new[] { "abc" }, returnValue: true);
            var output = sw.ToString();
            Assert.Contains("https://example.com/abc", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Data_ReturnsValueToStdout_WhenReturnFlagTrue()
    {
        var service = new ActionService();
        var key = new OKey
        {
            Key = "testdata",
            KeyType = OKeyType.Data,
            Value = "secret-value"
        };

        var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            await service.ExecuteAsync(key, Array.Empty<string>(), returnValue: true);
            var output = sw.ToString();
            Assert.Contains("secret-value", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task WebPath_ForceCopy_PrintsCopiedMessage()
    {
        var service = new ActionService();
        var key = new OKey
        {
            Key = "testweb2",
            KeyType = OKeyType.WebPath,
            Value = "https://example.com/"
        };

        var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            await service.ExecuteAsync(key, Array.Empty<string>(), forceCopy: true);
            var output = sw.ToString();
            Assert.Contains("copied to clipboard", output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
