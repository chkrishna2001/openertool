using System;
using System.Diagnostics;

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
    /// </summary>
    ProcessRunResult Run(string fileName, string[] arguments, string? standardInput = null);
}

/// <summary>
/// Default <see cref="IProcessRunner"/> implementation that actually spawns OS processes.
/// </summary>
public class SystemProcessRunner : IProcessRunner
{
    public bool CommandExists(string command)
    {
        try
        {
            var result = Run("which", new[] { command });
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput);
        }
        catch
        {
            return false;
        }
    }

    public ProcessRunResult Run(string fileName, string[] arguments, string? standardInput = null)
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

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessRunResult(process.ExitCode, stdout, stderr);
    }
}
