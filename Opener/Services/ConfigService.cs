using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Opener.Models;

namespace Opener.Services;

public interface IConfigService
{
    OpenerConfig GetConfig();
    void SaveConfig(OpenerConfig config);
    string GetDataFilePath();
    bool IsPortableMode();
}

public class ConfigService : IConfigService
{
    private readonly string _configDir;
    private readonly string _configPath;
    private OpenerConfig? _cachedConfig;
    
    // OneDrive retry configuration
    private const int MaxRetries = 5;
    private const int InitialRetryDelayMs = 200;

    public ConfigService()
    {
        var contextRoot = ExecutionContextHelper.GetExecutionContextPath();
        _configDir = Path.Combine(contextRoot, ".opener");
        _configPath = Path.Combine(_configDir, "config.json");
    }

    /// <summary>
    /// Detects execution context to avoid data collisions between dev, installed, and release scenarios.
    /// Returns the base path to use for config and data storage.
    /// </summary>
    private static string DetectExecutionContext()
    {
        // Check if running from a development context (dotnet run or local build)
        var currentExe = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        var currentDir = Path.GetDirectoryName(currentExe) ?? ".";
        
        // Detect: dotnet run or bin/Debug execution
        if (currentDir.Contains("bin" + Path.DirectorySeparatorChar + "Debug", StringComparison.OrdinalIgnoreCase) ||
            currentDir.Contains("bin/Debug", StringComparison.OrdinalIgnoreCase))
        {
            // Running in debug mode - use repo-local or temp storage to avoid global collision
            var repoRoot = FindRepositoryRoot(currentDir);
            if (!string.IsNullOrEmpty(repoRoot))
            {
                // Store in repo-local .dev folder
                return Path.Combine(repoRoot, ".dev-opener");
            }
        }

        // Detect: Release folder execution (e.g., publish output)
        if (currentDir.Contains("bin" + Path.DirectorySeparatorChar + "Release", StringComparison.OrdinalIgnoreCase) ||
            currentDir.Contains("bin/Release", StringComparison.OrdinalIgnoreCase))
        {
            // Running from release folder - use release-relative storage
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
    /// Finds the repository root by looking for .git or Opener.sln markers.
    /// Returns null if not found (indicating this is not a dev execution).
    /// </summary>
    private static string? FindRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        
        // Search up to 5 levels up the directory tree
        for (int i = 0; i < 5 && current != null; i++)
        {
            if (File.Exists(Path.Combine(current.FullName, ".git")))
                return current.FullName;
            
            if (File.Exists(Path.Combine(current.FullName, "Opener.sln")))
                return current.FullName;
            
            current = current.Parent;
        }

        return null;
    }

    public OpenerConfig GetConfig()
    {
        if (_cachedConfig != null) return _cachedConfig;

        if (!File.Exists(_configPath))
        {
            _cachedConfig = new OpenerConfig();
            return _cachedConfig;
        }

        try
        {
            string json = File.ReadAllText(_configPath);
            _cachedConfig = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.OpenerConfig) ?? new OpenerConfig();
            return _cachedConfig;
        }
        catch
        {
            _cachedConfig = new OpenerConfig();
            return _cachedConfig;
        }
    }

    public void SaveConfig(OpenerConfig config)
    {
        if (!Directory.Exists(_configDir))
        {
            Directory.CreateDirectory(_configDir);
        }

        string json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.OpenerConfig);
        File.WriteAllText(_configPath, json);
        _cachedConfig = config;
    }

    /// <summary>
    /// Detects if a path is on OneDrive (particularly company OneDrive with Microsoft 365).
    /// Company OneDrive paths typically contain "OneDrive" in the directory name.
    /// </summary>
    private static bool IsPathOneDrive(string path)
    {
        return path.IndexOf("OneDrive", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Creates a directory with retry logic for OneDrive paths.
    /// OneDrive's sync engine may need time to materialize directory structures.
    /// </summary>
    private static void CreateDirectoryWithRetry(string dirPath, bool isOneDrivePath = false)
    {
        if (Directory.Exists(dirPath))
        {
            return;
        }

        if (isOneDrivePath)
        {
            // OneDrive folders may be visible in Explorer but still reject CreateDirectory calls.
            // Do not try to materialize them from .NET; use the existing folder as-is.
            return;
        }

        if (!isOneDrivePath)
        {
            // Standard path - no retry needed
            Directory.CreateDirectory(dirPath);
            return;
        }

        // OneDrive path - use retry logic
        int retries = 0;
        int delayMs = InitialRetryDelayMs;

        while (retries < MaxRetries)
        {
            try
            {
                Directory.CreateDirectory(dirPath);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                retries++;
                if (retries >= MaxRetries)
                {
                    throw new Exception(
                        $"Unable to access OneDrive directory after {MaxRetries} attempts. " +
                        $"Ensure the OneDrive path exists and sync is complete: {dirPath}");
                }

                Thread.Sleep(delayMs);
                delayMs = Math.Min(delayMs + 300, 2000); // Exponential backoff, max 2 sec
            }
            catch (Exception)
            {
                throw;
            }
        }
    }

    public string GetDataFilePath()
    {
        var config = GetConfig();
        
        if (!string.IsNullOrEmpty(config.StorageLocation))
        {
            // Custom storage location (e.g. OneDrive)
            var dir = Path.GetDirectoryName(config.StorageLocation);
            if (!string.IsNullOrEmpty(dir))
            {
                bool isOneDrive = IsPathOneDrive(config.StorageLocation);
                if (!isOneDrive)
                {
                    try
                    {
                        CreateDirectoryWithRetry(dir, isOneDrive);
                    }
                    catch (Exception ex)
                    {
                        // Log warning but don't crash - the caller (StorageService.SaveKeys) 
                        // will handle the error when it actually tries to write
                        Console.Error.WriteLine($"Warning: Could not ensure directory exists: {ex.Message}");
                    }
                }
            }
            return config.StorageLocation;
        }

        // Default location
        var dataRoot = Environment.GetEnvironmentVariable("OPENER_DATA_DIR");
        var appData = string.IsNullOrWhiteSpace(dataRoot)
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : dataRoot;

        var folder = Path.Combine(appData, "Opener");
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        return Path.Combine(folder, "opener.dat");
    }

    public bool IsPortableMode()
    {
        return GetConfig().EncryptionMode.Equals("portable", StringComparison.OrdinalIgnoreCase);
    }
}
