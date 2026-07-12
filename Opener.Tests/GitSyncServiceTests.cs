using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Opener.Models;
using Opener.Services;
using Xunit;

namespace Opener.Tests;

public class GitSyncServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string _syncRepoDir;
    private readonly string _dataFile;
    private readonly Mock<IConfigService> _configMock = new();
    private readonly Mock<IProcessRunner> _runnerMock = new();
    private readonly Mock<ICredentialService> _gitCredMock = new();

    public GitSyncServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "opener-gitsync-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _syncRepoDir = Path.Combine(_tempRoot, "sync-repo");
        _dataFile = Path.Combine(_tempRoot, "opener.dat");
        File.WriteAllText(_dataFile, "mock-encrypted-vault-content");

        _configMock.Setup(c => c.GetDataFilePath()).Returns(_dataFile);
        _runnerMock.Setup(r => r.CommandExists("git")).Returns(true);
        _runnerMock.Setup(r => r.Run("git", It.IsAny<string[]>(), null)).Returns(new ProcessRunResult(0, "", ""));
    }

    private GitSyncService CreateService() =>
        new GitSyncService(_configMock.Object, _runnerMock.Object, _syncRepoDir, _gitCredMock.Object);

    [Fact]
    public async Task PushAsync_NoRemoteConfigured_ReturnsFailureWithoutCallingGit()
    {
        _configMock.Setup(c => c.GetConfig()).Returns(new OpenerConfig());
        var service = CreateService();

        var result = await service.PushAsync();

        Assert.False(result.Success);
        Assert.Contains("no sync remote", result.Message, StringComparison.OrdinalIgnoreCase);
        _runnerMock.Verify(r => r.Run(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task PushAsync_GitNotOnPath_ReturnsFailure()
    {
        _configMock.Setup(c => c.GetConfig()).Returns(new OpenerConfig { GitSyncRemote = "git@github.com:me/vault.git" });
        _runnerMock.Setup(r => r.CommandExists("git")).Returns(false);
        var service = CreateService();

        var result = await service.PushAsync();

        Assert.False(result.Success);
        Assert.Contains("git", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PushAsync_NoVaultFileYet_ReturnsFailure()
    {
        File.Delete(_dataFile);
        _configMock.Setup(c => c.GetConfig()).Returns(new OpenerConfig { GitSyncRemote = "git@github.com:me/vault.git" });
        var service = CreateService();

        var result = await service.PushAsync();

        Assert.False(result.Success);
        Assert.Contains("no vault file", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PushAsync_SshRemote_InitializesRepoAndPushesWithoutAuthHeader()
    {
        _configMock.Setup(c => c.GetConfig()).Returns(new OpenerConfig { GitSyncRemote = "git@github.com:me/vault.git" });
        var service = CreateService();

        var result = await service.PushAsync();

        Assert.True(result.Success);
        _runnerMock.Verify(r => r.Run("git", It.Is<string[]>(a => a.Contains("init")), null), Times.Once);
        _runnerMock.Verify(r => r.Run("git", It.Is<string[]>(a =>
            a.Contains("push") && !a.Any(x => x.Contains("extraheader"))), null), Times.Once);
        Assert.True(File.Exists(Path.Combine(_syncRepoDir, "opener.dat")));
    }

    [Fact]
    public async Task PushAsync_HttpsRemoteWithStoredToken_InjectsAuthHeaderPerInvocationOnlyAndNeverPersistsIt()
    {
        _configMock.Setup(c => c.GetConfig()).Returns(new OpenerConfig { GitSyncRemote = "https://github.com/me/vault.git" });
        _gitCredMock.Setup(c => c.GetPassword()).Returns("ghp_supersecrettoken");
        var service = CreateService();

        var result = await service.PushAsync();

        Assert.True(result.Success);
        _runnerMock.Verify(r => r.Run("git", It.Is<string[]>(a =>
            a.Contains("push") && a.Any(x => x.StartsWith("http.extraheader="))), null), Times.Once);

        // The token must only ever appear in the in-memory process arguments for that one
        // call - never written into any file under the sync repo (e.g. via git config).
        foreach (var file in Directory.GetFiles(_syncRepoDir, "*", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("ghp_supersecrettoken", File.ReadAllText(file));
        }
    }

    [Fact]
    public async Task PushAsync_HttpsRemoteWithoutStoredToken_PushesWithoutAuthHeader()
    {
        _configMock.Setup(c => c.GetConfig()).Returns(new OpenerConfig { GitSyncRemote = "https://github.com/me/vault.git" });
        _gitCredMock.Setup(c => c.GetPassword()).Returns((string?)null);
        var service = CreateService();

        var result = await service.PushAsync();

        Assert.True(result.Success);
        _runnerMock.Verify(r => r.Run("git", It.Is<string[]>(a =>
            a.Contains("push") && !a.Any(x => x.Contains("extraheader"))), null), Times.Once);
    }

    [Fact]
    public async Task PullAsync_ConflictOrFailure_AbortsMergeAndDoesNotTouchLocalVault()
    {
        _configMock.Setup(c => c.GetConfig()).Returns(new OpenerConfig { GitSyncRemote = "git@github.com:me/vault.git" });
        _runnerMock.Setup(r => r.Run("git", It.Is<string[]>(a => a.Contains("pull")), null))
            .Returns(new ProcessRunResult(1, "", "CONFLICT (add/add): merge conflict in opener.dat"));
        var originalContent = File.ReadAllText(_dataFile);
        var service = CreateService();

        var result = await service.PullAsync();

        Assert.False(result.Success);
        _runnerMock.Verify(r => r.Run("git", It.Is<string[]>(a => a.Contains("merge") && a.Contains("--abort")), null), Times.Once);
        Assert.Equal(originalContent, File.ReadAllText(_dataFile));
    }

    [Fact]
    public async Task PullAsync_Success_BacksUpCurrentVaultBeforeOverwriting()
    {
        _configMock.Setup(c => c.GetConfig()).Returns(new OpenerConfig { GitSyncRemote = "git@github.com:me/vault.git" });
        _runnerMock.Setup(r => r.Run("git", It.Is<string[]>(a => a.Contains("pull")), null))
            .Returns<string, string[], string?>((_, _, _) =>
            {
                Directory.CreateDirectory(_syncRepoDir);
                File.WriteAllText(Path.Combine(_syncRepoDir, "opener.dat"), "pulled-content-from-remote");
                return new ProcessRunResult(0, "", "");
            });

        var service = CreateService();
        var result = await service.PullAsync();

        Assert.True(result.Success);
        Assert.Equal("pulled-content-from-remote", File.ReadAllText(_dataFile));

        var backupDir = Path.Combine(_tempRoot, ".backup");
        Assert.True(Directory.Exists(backupDir));
        var backups = Directory.GetFiles(backupDir, "opener_backup_*.dat");
        Assert.Single(backups);
        Assert.Equal("mock-encrypted-vault-content", File.ReadAllText(backups[0]));
    }

    [Fact]
    public async Task PullAsync_NoRemoteConfigured_ReturnsFailureWithoutCallingGit()
    {
        _configMock.Setup(c => c.GetConfig()).Returns(new OpenerConfig());
        var service = CreateService();

        var result = await service.PullAsync();

        Assert.False(result.Success);
        _runnerMock.Verify(r => r.Run(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void IsConfigured_RequiresBothRemoteAndGitOnPath()
    {
        _configMock.Setup(c => c.GetConfig()).Returns(new OpenerConfig { GitSyncRemote = "git@github.com:me/vault.git" });
        var service = CreateService();

        Assert.True(service.IsConfigured());

        _runnerMock.Setup(r => r.CommandExists("git")).Returns(false);
        Assert.False(service.IsConfigured());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
