using System;
using Opener.Services;
using Xunit;

namespace Opener.Tests;

public class EncryptionServiceTests
{
    [Fact]
    public void PortableEncryption_EncryptAndDecrypt_ReturnsOriginalText()
    {
        // Arrange
        var password = "StrongPassword123!";
        var service = new PortableEncryptionService(password);
        var originalText = "Hello World - Sensitive Data 123";

        // Act
        var encrypted = service.Encrypt(originalText);
        var decrypted = service.Decrypt(encrypted);

        // Assert
        Assert.NotEqual(originalText, encrypted);
        Assert.Equal(originalText, decrypted);
    }

    [Fact]
    public void PortableEncryption_WithWrongPassword_ThrowsException()
    {
        // Arrange
        var service1 = new PortableEncryptionService("pass1");
        var service2 = new PortableEncryptionService("pass2");
        var originalText = "Secret";

        // Act
        var encrypted = service1.Encrypt(originalText);

        // Assert
        Assert.Throws<InvalidOperationException>(() => service2.Decrypt(encrypted));
    }
}
