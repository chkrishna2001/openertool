using System;
using System.IO;
using Moq;
using Opener.Services;
using Xunit;

namespace Opener.Tests;

public class CredentialServiceTests
{
    // ---------- SecretToolCredentialService (Linux / secret-tool) ----------

    [Fact]
    public void SecretTool_WhenCliPresent_GetPassword_ParsesLookupOutput()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.CommandExists("secret-tool")).Returns(true);
        runner.Setup(r => r.Run("secret-tool", It.Is<string[]>(a =>
                a.Length == 5 && a[0] == "lookup" && a[1] == "service" && a[2] == "opener" && a[3] == "username"),
                null))
            .Returns(new ProcessRunResult(0, "super-secret\n", ""));

        var fallback = new Mock<ICredentialService>();
        var service = new SecretToolCredentialService(runner.Object, fallback.Object);

        var result = service.GetPassword();

        Assert.Equal("super-secret", result);
        fallback.Verify(f => f.GetPassword(), Times.Never);
    }

    [Fact]
    public void SecretTool_WhenCliPresent_SetPassword_StoresViaStdin()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.CommandExists("secret-tool")).Returns(true);
        runner.Setup(r => r.Run("secret-tool", It.Is<string[]>(a =>
                a[0] == "store" && a[1] == "--label=Opener CLI" && a[2] == "service" && a[3] == "opener" && a[4] == "username"),
                "my-password"))
            .Returns(new ProcessRunResult(0, "", ""));

        var fallback = new Mock<ICredentialService>();
        var service = new SecretToolCredentialService(runner.Object, fallback.Object);

        service.SetPassword("my-password");

        runner.Verify(r => r.Run("secret-tool", It.IsAny<string[]>(), "my-password"), Times.Once);
        fallback.Verify(f => f.SetPassword(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void SecretTool_WhenCliPresent_ClearPassword_CallsClear()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.CommandExists("secret-tool")).Returns(true);
        runner.Setup(r => r.Run("secret-tool", It.Is<string[]>(a => a[0] == "clear"), null))
            .Returns(new ProcessRunResult(0, "", ""));

        var fallback = new Mock<ICredentialService>();
        var service = new SecretToolCredentialService(runner.Object, fallback.Object);

        service.ClearPassword();

        runner.Verify(r => r.Run("secret-tool", It.Is<string[]>(a => a[0] == "clear"), null), Times.Once);
    }

    [Fact]
    public void SecretTool_WhenCliAbsent_DelegatesToFallback()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.CommandExists("secret-tool")).Returns(false);

        var fallback = new Mock<ICredentialService>();
        fallback.Setup(f => f.GetPassword()).Returns("fallback-pass");

        var service = new SecretToolCredentialService(runner.Object, fallback.Object);

        Assert.Equal("fallback-pass", service.GetPassword());
        service.SetPassword("abc");
        service.ClearPassword();

        fallback.Verify(f => f.GetPassword(), Times.Once);
        fallback.Verify(f => f.SetPassword("abc"), Times.Once);
        fallback.Verify(f => f.ClearPassword(), Times.Once);
        runner.Verify(r => r.Run(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void SecretTool_WhenStoreFails_FallsBackToFile()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.CommandExists("secret-tool")).Returns(true);
        runner.Setup(r => r.Run("secret-tool", It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns(new ProcessRunResult(1, "", "no keyring daemon"));

        var fallback = new Mock<ICredentialService>();
        var service = new SecretToolCredentialService(runner.Object, fallback.Object);

        service.SetPassword("abc");

        fallback.Verify(f => f.SetPassword("abc"), Times.Once);
    }

    // ---------- MacKeychainCredentialService (macOS / security) ----------

    [Fact]
    public void MacKeychain_WhenCliPresent_GetPassword_ParsesFindOutput()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.CommandExists("security")).Returns(true);
        runner.Setup(r => r.Run("security", It.Is<string[]>(a =>
                a[0] == "find-generic-password" && a[1] == "-a" && a[3] == "-s" && a[4] == "opener" && a[5] == "-w"),
                null))
            .Returns(new ProcessRunResult(0, "super-secret\n", ""));

        var fallback = new Mock<ICredentialService>();
        var service = new MacKeychainCredentialService(runner.Object, fallback.Object);

        var result = service.GetPassword();

        Assert.Equal("super-secret", result);
        fallback.Verify(f => f.GetPassword(), Times.Never);
    }

    [Fact]
    public void MacKeychain_WhenCliPresent_SetPassword_CallsAddGenericPassword()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.CommandExists("security")).Returns(true);
        runner.Setup(r => r.Run("security", It.Is<string[]>(a =>
                a[0] == "add-generic-password" && a[1] == "-a" && a[3] == "-s" && a[4] == "opener" && a[5] == "-w" && a[6] == "my-password" && a[7] == "-U"),
                null))
            .Returns(new ProcessRunResult(0, "", ""));

        var fallback = new Mock<ICredentialService>();
        var service = new MacKeychainCredentialService(runner.Object, fallback.Object);

        service.SetPassword("my-password");

        fallback.Verify(f => f.SetPassword(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void MacKeychain_WhenCliAbsent_DelegatesToFallback()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.CommandExists("security")).Returns(false);

        var fallback = new Mock<ICredentialService>();
        fallback.Setup(f => f.GetPassword()).Returns("fallback-pass");

        var service = new MacKeychainCredentialService(runner.Object, fallback.Object);

        Assert.Equal("fallback-pass", service.GetPassword());
        service.SetPassword("abc");
        service.ClearPassword();

        fallback.Verify(f => f.GetPassword(), Times.Once);
        fallback.Verify(f => f.SetPassword("abc"), Times.Once);
        fallback.Verify(f => f.ClearPassword(), Times.Once);
        runner.Verify(r => r.Run(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<string?>()), Times.Never);
    }

    // ---------- Purpose separation (e.g. vault unlock password vs git-sync token) ----------

    [Fact]
    public void SecretTool_DifferentPurposes_UseDifferentServiceAttribute()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.CommandExists("secret-tool")).Returns(true);
        runner.Setup(r => r.Run("secret-tool", It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns(new ProcessRunResult(0, "", ""));

        var vaultService = new SecretToolCredentialService(runner.Object, new Mock<ICredentialService>().Object, purpose: "vault");
        var gitSyncService = new SecretToolCredentialService(runner.Object, new Mock<ICredentialService>().Object, purpose: "git-sync");

        vaultService.SetPassword("vault-secret");
        gitSyncService.SetPassword("git-token");

        runner.Verify(r => r.Run("secret-tool", It.Is<string[]>(a => a.Contains("opener")), "vault-secret"), Times.Once);
        runner.Verify(r => r.Run("secret-tool", It.Is<string[]>(a => a.Contains("opener-git-sync")), "git-token"), Times.Once);
    }

    [Fact]
    public void MacKeychain_DifferentPurposes_UseDifferentServiceName()
    {
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.CommandExists("security")).Returns(true);
        runner.Setup(r => r.Run("security", It.IsAny<string[]>(), It.IsAny<string?>()))
            .Returns(new ProcessRunResult(0, "", ""));

        var vaultService = new MacKeychainCredentialService(runner.Object, new Mock<ICredentialService>().Object, purpose: "vault");
        var gitSyncService = new MacKeychainCredentialService(runner.Object, new Mock<ICredentialService>().Object, purpose: "git-sync");

        vaultService.SetPassword("vault-secret");
        gitSyncService.SetPassword("git-token");

        runner.Verify(r => r.Run("security", It.Is<string[]>(a => a.Contains("opener")), null), Times.Once);
        runner.Verify(r => r.Run("security", It.Is<string[]>(a => a.Contains("opener-git-sync")), null), Times.Once);
    }

}

// ---------- FileCredentialService (encrypted fallback file) ----------

public class FileCredentialServiceTests : IDisposable
{
    private readonly string _tempHome;

    public FileCredentialServiceTests()
    {
        _tempHome = Path.Combine(Path.GetTempPath(), "opener-cred-test-" + Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public void SetAndGetPassword_RoundTrips()
    {
        var service = new FileCredentialService(_tempHome);

        service.SetPassword("hunter2");
        var result = service.GetPassword();

        Assert.Equal("hunter2", result);
    }

    [Fact]
    public void SetPassword_DoesNotStorePlainText()
    {
        var service = new FileCredentialService(_tempHome);
        service.SetPassword("hunter2");

        var filePath = Path.Combine(_tempHome, ".opener", ".internal_pass");
        Assert.True(File.Exists(filePath));

        var rawContents = File.ReadAllText(filePath);
        Assert.DoesNotContain("hunter2", rawContents);
    }

    [Fact]
    public void GetPassword_WhenNoFileExists_ReturnsNull()
    {
        var service = new FileCredentialService(_tempHome);
        Assert.Null(service.GetPassword());
    }

    [Fact]
    public void ClearPassword_RemovesFile()
    {
        var service = new FileCredentialService(_tempHome);
        service.SetPassword("hunter2");
        service.ClearPassword();

        Assert.Null(service.GetPassword());
    }

    [Fact]
    public void DifferentPurposes_DoNotShareAStorageSlot()
    {
        var vaultService = new FileCredentialService(_tempHome, purpose: "vault");
        var gitSyncService = new FileCredentialService(_tempHome, purpose: "git-sync");

        vaultService.SetPassword("vault-secret");
        gitSyncService.SetPassword("git-token");

        Assert.Equal("vault-secret", vaultService.GetPassword());
        Assert.Equal("git-token", gitSyncService.GetPassword());

        gitSyncService.ClearPassword();
        Assert.Null(gitSyncService.GetPassword());
        Assert.Equal("vault-secret", vaultService.GetPassword());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempHome))
        {
            try { Directory.Delete(_tempHome, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
