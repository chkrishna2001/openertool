using System.Collections.Generic;
using System.IO;
using Moq;
using Opener.Models;
using Opener.Services;
using Xunit;

namespace Opener.Tests;

public class StorageServiceTests
{
    private readonly Mock<IEncryptionService> _mockEncryptor;
    private readonly Mock<IConfigService> _mockConfig;
    private readonly string _tempFile;

    public StorageServiceTests()
    {
        _mockEncryptor = new Mock<IEncryptionService>();
        _mockConfig = new Mock<IConfigService>();
        _tempFile = Path.GetTempFileName();
        _mockConfig.Setup(c => c.GetDataFilePath()).Returns(_tempFile);
    }

    [Fact]
    public void SaveAndGetKeys_WorksCorrectly()
    {
        // Arrange
        var keys = new List<OKey>
        {
            new OKey { Key = "k1", Value = "v1" }
        };

        // Identity encryption for testing
        _mockEncryptor.Setup(e => e.Encrypt(It.IsAny<string>())).Returns((string s) => s);
        _mockEncryptor.Setup(e => e.Decrypt(It.IsAny<string>())).Returns((string s) => s);

        var service = new StorageService(_mockConfig.Object, _mockEncryptor.Object);

        // Act
        service.SaveKeys(keys);
        var result = service.GetKeys();

        // Assert
        Assert.Single(result);
        Assert.Equal("k1", result[0].Key);
        
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }
}
