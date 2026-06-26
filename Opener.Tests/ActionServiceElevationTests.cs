using System;
using System.IO;
using System.Threading.Tasks;
using Opener.Models;
using Opener.Services;
using Xunit;

namespace Opener.Tests;

[Collection("ConsoleTests")]
public class ActionServiceElevationTests
{
    [Fact]
    public void OKey_ElevatedProperty_DefaultsToFalse()
    {
        var key = new OKey();
        Assert.False(key.Elevated);
    }

    [Fact]
    public void OKey_ElevatedProperty_CanBeSetToTrue()
    {
        var key = new OKey
        {
            Key = "testkey",
            Value = "testvalue",
            Elevated = true
        };
        Assert.True(key.Elevated);
    }

    [Fact]
    public async Task LocalPath_ReturnsValueToStdout_WhenReturnFlagTrue_AndElevatedTrue()
    {
        var service = new ActionService();
        var key = new OKey
        {
            Key = "testscript",
            KeyType = OKeyType.LocalPath,
            Value = "C:\\path\\to\\script.bat",
            Elevated = true
        };

        var sw = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            await service.ExecuteAsync(key, Array.Empty<string>(), returnValue: true, elevated: true);
            var output = sw.ToString();
            Assert.Contains("C:\\path\\to\\script.bat", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
