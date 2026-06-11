using System;
using System.IO;

namespace Opener.Services;

/// <summary>
/// Detects execution context (dev, release, installed tool) to avoid data collisions
/// between different execution scenarios.
/// </summary>
public static class ExecutionContextHelper
{
    /// <summary>
    /// Detects execution context and returns the appropriate base path for storage.
    /// Priority:
    /// 1. OPENER_HOME environment variable (explicit override)
    /// 2. Auto-detect dev/release/installed context
    /// 3. Default to user profile (global tool)
    /// </summary>
    public static string GetExecutionContextPath()
    {
        // Priority 1: Explicit override
        var openerHome = Environment.GetEnvironmentVariable("OPENER_HOME");
        if (!string.IsNullOrWhiteSpace(openerHome))
        {
            return openerHome;
        }

        // Priority 2: Auto-detect context
        var contextPath = DetectExecutionContext();
        return contextPath;
    }

    /// <summary>
    /// Detects the current execution context and returns the appropriate storage root.
    /// </summary>
    private static string DetectExecutionContext()
    {
        var currentExe = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        var currentDir = Path.GetDirectoryName(currentExe) ?? ".";
        
        // Detect: dotnet run or bin/Debug execution
        if (IsDebugExecution(currentDir))
        {
            var repoRoot = FindRepositoryRoot(currentDir);
            if (!string.IsNullOrEmpty(repoRoot))
            {
                // Store in repo-local .dev folder
                return Path.Combine(repoRoot, ".dev-opener");
            }
        }

        // Detect: Release folder execution
        if (IsReleaseExecution(currentDir))
        {
            var repoRoot = FindRepositoryRoot(currentDir);
            if (!string.IsNullOrEmpty(repoRoot))
            {
                return Path.Combine(repoRoot, ".release-opener");
            }
        }

        // Default: Global user context (installed tool or regular execution)
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>
    /// Checks if currently running from a Debug build directory.
    /// </summary>
    private static bool IsDebugExecution(string currentDir)
    {
        return currentDir.Contains("bin" + Path.DirectorySeparatorChar + "Debug", StringComparison.OrdinalIgnoreCase) ||
               currentDir.Contains("bin/Debug", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if currently running from a Release build directory.
    /// </summary>
    private static bool IsReleaseExecution(string currentDir)
    {
        return currentDir.Contains("bin" + Path.DirectorySeparatorChar + "Release", StringComparison.OrdinalIgnoreCase) ||
               currentDir.Contains("bin/Release", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Finds the repository root by looking for .git or Opener.sln markers.
    /// Returns null if not found.
    /// </summary>
    private static string? FindRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        
        // Search up to 10 levels up the directory tree
        for (int i = 0; i < 10 && current != null; i++)
        {
            if (File.Exists(Path.Combine(current.FullName, ".git")))
                return current.FullName;
            
            if (File.Exists(Path.Combine(current.FullName, "Opener.sln")))
                return current.FullName;
            
            current = current.Parent;
        }

        return null;
    }
}
