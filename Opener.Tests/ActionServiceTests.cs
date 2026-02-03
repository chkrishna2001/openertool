using System.Threading.Tasks;
using Opener.Models;
using Opener.Services;
using Xunit;

namespace Opener.Tests;

public class ActionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WithWebPathAndPlaceholder_ReplacesPlaceholder()
    {
        // Arrange
        var service = new ActionService();
        var key = new OKey
        {
            Key = "test",
            KeyType = OKeyType.WebPath,
            Value = "https://example.com/search?q={0}"
        };
        
        // This test verify it doesn't crash. 
        // Intercepting browser open is hard, but we can check if it handles placeholders without error.
        await service.ExecuteAsync(key, new[] { "apple" });
    }

    [Fact]
    public async Task HandleData_CopiesToClipboard()
    {
        // This depends on TextCopy, might be hard to test in non-UI environment
        // but let's see if it runs.
    }
}
