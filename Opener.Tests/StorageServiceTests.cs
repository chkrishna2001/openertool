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

    [Fact]
    public void Initialize_DoesNotThrow_WhenStoragePathInaccessible()
    {
        // Arrange
        var inaccessiblePath = Path.Combine(Path.GetTempPath(), "inaccessible_" + Guid.NewGuid(), "opener.dat");
        _mockConfig.Setup(c => c.GetDataFilePath()).Returns(inaccessiblePath);
        
        _mockEncryptor.Setup(e => e.Encrypt(It.IsAny<string>())).Returns((string s) => s);
        _mockEncryptor.Setup(e => e.Decrypt(It.IsAny<string>())).Returns((string s) => s);

        var service = new StorageService(_mockConfig.Object, _mockEncryptor.Object);

        // Act - Should not throw even though path is inaccessible
        var exception = Record.Exception(() => service.Initialize());

        // Assert - Initialize should log warning but not crash
        Assert.Null(exception);
    }

    [Fact]
    public void SaveKeys_WithNormalPath_WorksCorrectly()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "opener_test_" + Guid.NewGuid());
        var filePath = Path.Combine(tempDir, "opener.dat");
        
        try
        {
            _mockConfig.Setup(c => c.GetDataFilePath()).Returns(filePath);
            
            _mockEncryptor.Setup(e => e.Encrypt(It.IsAny<string>())).Returns((string s) => s);
            _mockEncryptor.Setup(e => e.Decrypt(It.IsAny<string>())).Returns((string s) => s);

            var keys = new List<OKey>
            {
                new OKey { Key = "test", Value = "value" }
            };

            var service = new StorageService(_mockConfig.Object, _mockEncryptor.Object);

            // Act
            service.SaveKeys(keys);

            // Assert
            Assert.True(File.Exists(filePath));
            var loaded = service.GetKeys();
            Assert.Single(loaded);
            Assert.Equal("test", loaded[0].Key);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
