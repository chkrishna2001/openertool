using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Opener.Services;
using Xunit;

namespace Opener.Tests;

public class ProcessRunnerTests
{
    // A command that reliably outlives the short timeout below, on every platform this
    // runs on in CI (Windows has no `sleep`; Linux/macOS have no `ping -n`).
    private static (string file, string[] args) SlowCommand()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ("ping", new[] { "-n", "30", "127.0.0.1" })
            : ("sleep", new[] { "30" });
    }

    [Fact]
    public void Run_ProcessExceedsTimeout_ThrowsInsteadOfHangingForever()
    {
        // This is the exact failure mode that caused a CI pipeline to hang indefinitely:
        // a keychain/credential-store CLI (secret-tool, security) blocking on an
        // interactive authorization prompt that never comes in a headless environment.
        // Without a timeout, IProcessRunner.Run would block forever; every real call site
        // (SecretToolCredentialService, MacKeychainCredentialService, GitSyncService)
        // already catches exceptions and falls back, so turning a hang into a bounded
        // exception is a complete fix - if this test doesn't return well within the test
        // runner's own timeout, the fix doesn't work.
        var runner = new SystemProcessRunner();
        var (file, args) = SlowCommand();

        var stopwatch = Stopwatch.StartNew();
        var ex = Assert.Throws<TimeoutException>(() => runner.Run(file, args, timeout: TimeSpan.FromMilliseconds(500)));
        stopwatch.Stop();

        Assert.Contains(file, ex.Message);
        // Generous upper bound so this isn't flaky under CI load, while still proving the
        // process was killed rather than left to run its full ~30s course.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Expected the process to be killed near the 500ms timeout, but Run() took {stopwatch.Elapsed}.");
    }

    [Fact]
    public void Run_FastProcess_ReturnsNormallyWithinDefaultTimeout()
    {
        var runner = new SystemProcessRunner();

        var result = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? runner.Run("cmd", new[] { "/c", "echo hello" })
            : runner.Run("echo", new[] { "hello" });

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.StandardOutput);
    }
}
