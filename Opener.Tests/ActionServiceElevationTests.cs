using System;
using System.IO;
using System.Threading.Tasks;
using Opener.Models;
using Opener.Services;
using Spectre.Console;
using Spectre.Console.Testing;
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

    [Fact]
    public async Task LocalPath_ReportsError_WhenElevatedLaunchFails_InsteadOfSilentlyFallingBackUnelevated()
    {
        // Windows-only: a bad elevated path fails synchronously here (ShellExecuteEx with
        // Verb=runas throws immediately for a nonexistent file), which is the scenario this
        // test exercises. On Linux/macOS the elevated branch shells out to "sudo" (which
        // exists on the runner and starts successfully) - any "path not found" failure
        // happens asynchronously inside the spawned sudo process, not as a .NET exception,
        // so there's nothing synchronous to assert on there.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = new ActionService();
        var key = new OKey
        {
            Key = "testscript",
            KeyType = OKeyType.LocalPath,
            // Nonexistent path so Process.Start throws instead of actually launching anything.
            Value = "C:\\this\\path\\does\\not\\exist\\script.bat",
            Elevated = true
        };

        var testConsole = new TestConsole();
        var originalConsole = AnsiConsole.Console;
        try
        {
            AnsiConsole.Console = testConsole;
            await service.ExecuteAsync(key, Array.Empty<string>(), elevated: true);
            var output = testConsole.Output;

            Assert.Contains("Failed to launch elevated", output);
            // Must not have silently downgraded to a non-elevated open, which would
            // print the OpenUrl fallback's error message instead.
            Assert.DoesNotContain("Could not open", output);
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    [Fact]
    public void BuildWindowsLocalPathProcessStartInfo_Ps1Script_LaunchesThroughPowerShellExecutable()
    {
        // Windows' "runas" verb only applies to executables - ShellExecute-ing a .ps1
        // directly just performs its default (edit-in-text-editor) action and silently
        // drops the elevation request. So .ps1 scripts must be launched via the resolved
        // PowerShell executable (pwsh.exe if present, else powershell.exe) with -File,
        // instead of setting FileName to the script path directly.
        var psi = ActionService.BuildWindowsLocalPathProcessStartInfo(
            "C:\\scripts\\deploy.ps1", new[] { "arg1" }, elevated: true);

        Assert.Equal(ActionService.ResolvePowerShellExecutable(), psi.FileName);
        Assert.Equal("runas", psi.Verb);
        Assert.Contains("-File", psi.ArgumentList);
        Assert.Contains("C:\\scripts\\deploy.ps1", psi.ArgumentList);
        Assert.Contains("arg1", psi.ArgumentList);
    }

    [Fact]
    public void ResolvePowerShellExecutable_ReturnsPwshOrPowerShell_NeverHardcodedToOne()
    {
        var resolved = ActionService.ResolvePowerShellExecutable();
        Assert.True(resolved == "pwsh.exe" || resolved == "powershell.exe");
    }

    [Fact]
    public void BuildWindowsLocalPathProcessStartInfo_NonPs1Script_LaunchesFileDirectly()
    {
        var psi = ActionService.BuildWindowsLocalPathProcessStartInfo(
            "C:\\scripts\\deploy.bat", new[] { "arg1" }, elevated: true);

        Assert.Equal("C:\\scripts\\deploy.bat", psi.FileName);
        Assert.Equal("runas", psi.Verb);
        Assert.Contains("arg1", psi.ArgumentList);
    }

    [Fact]
    public void BuildWindowsLocalPathProcessStartInfo_NotElevated_LeavesVerbUnset()
    {
        var psi = ActionService.BuildWindowsLocalPathProcessStartInfo(
            "C:\\scripts\\deploy.ps1", Array.Empty<string>(), elevated: false);

        Assert.Equal(ActionService.ResolvePowerShellExecutable(), psi.FileName);
        Assert.True(string.IsNullOrEmpty(psi.Verb));
    }
}
