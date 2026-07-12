using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Opener.Services;

/// <summary>
/// Result of running an external process to completion.
/// </summary>
public readonly record struct ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Thin abstraction over spawning external processes, so callers (e.g. keychain-backed
/// credential services that shell out to CLI tools like secret-tool/security) can be
/// unit tested without invoking real OS binaries.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Returns true if the given command is resolvable on PATH.
    /// </summary>
    bool CommandExists(string command);

    /// <summary>
    /// Runs the given command with the given arguments, optionally piping
    /// <paramref name="standardInput"/> to the process's stdin, and waits for exit.
    /// Bounded by <paramref name="timeout"/> - if the process doesn't exit in time it's
    /// killed and a <see cref="TimeoutException"/> is thrown, rather than blocking forever.
    /// This matters most in headless environments (e.g. CI): a keychain/credential-store
    /// CLI (secret-tool, security) can block waiting for an interactive authorization
    /// prompt that will never come.
    /// </summary>
    ProcessRunResult Run(string fileName, string[] arguments, string? standardInput = null, TimeSpan? timeout = null);
}

/// <summary>
/// Default <see cref="IProcessRunner"/> implementation that actually spawns OS processes.
/// </summary>
public class SystemProcessRunner : IProcessRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    public bool CommandExists(string command)
    {
        try
        {
            var lookup = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
            var result = Run(lookup, new[] { command });
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
        }
        catch
        {
            return false;
        }
    }

    public ProcessRunResult Run(string fileName, string[] arguments, string? standardInput = null, TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput != null,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException($"Failed to start process: {fileName}");
        }

        if (standardInput != null)
        {
            process.StandardInput.Write(standardInput);
            process.StandardInput.Close();
        }

        // Read both streams concurrently (not sequential ReadToEnd calls) to avoid a
        // classic pipe deadlock: a process can block writing to a full stderr pipe while
        // we're only draining stdout, or vice versa.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        var effectiveTimeout = timeout ?? DefaultTimeout;
        if (!process.WaitForExit((int)effectiveTimeout.TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            throw new TimeoutException($"'{fileName}' timed out after {effectiveTimeout.TotalSeconds}s and was terminated.");
        }

        string stdout = stdoutTask.GetAwaiter().GetResult();
        string stderr = stderrTask.GetAwaiter().GetResult();

        return new ProcessRunResult(process.ExitCode, stdout, stderr);
    }
}
