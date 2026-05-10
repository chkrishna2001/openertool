using System.Threading.Tasks;
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
}
