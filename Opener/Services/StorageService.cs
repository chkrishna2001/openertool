using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using Opener.Models;

namespace Opener.Services;

public interface IStorageService
{
    void Initialize();
    List<OKey> GetKeys();
    void SaveKeys(List<OKey> keys);
    string GetFilePath();
}

public class StorageService : IStorageService
{
    private readonly IConfigService _configService;
    private readonly IEncryptionService _encryptionService;
    
    // OneDrive retry configuration
    private const int MaxRetries = 5;
    private const int InitialRetryDelayMs = 200;
    
    // Fallback storage location for when OneDrive fails
    private string? _fallbackPath;
    private bool _usingFallback = false;

    public StorageService(IConfigService configService, IEncryptionService encryptionService)
    {
        _configService = configService;
        _encryptionService = encryptionService;
    }

    public string GetFilePath()
    {
        if (_usingFallback && !string.IsNullOrEmpty(_fallbackPath))
        {
            return _fallbackPath;
        }
        return _configService.GetDataFilePath();
    }
    
    private string GetFallbackPath()
    {
        var fallbackDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".opener",
            "cache"
        );
        return Path.Combine(fallbackDir, "opener.dat");
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
    /// Ensures a directory exists with retry logic for OneDrive paths.
    /// OneDrive's sync engine may need time to materialize directory structures.
    /// </summary>
    private static void EnsureDirectoryExistsWithRetry(string path, bool isOneDrivePath = false)
    {
        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir) || Directory.Exists(dir))
        {
            return;
        }

        if (isOneDrivePath)
        {
            // OneDrive-synced folders can appear available in Explorer while directory creation
            // from .NET still fails. Skip programmatic creation and rely on the existing folder.
            return;
        }

        if (!isOneDrivePath)
        {
            // Standard path - no retry needed
            Directory.CreateDirectory(dir);
            return;
        }

        // OneDrive path - use retry logic
        int retries = 0;
        int delayMs = InitialRetryDelayMs;

        while (retries < MaxRetries)
        {
            try
            {
                Directory.CreateDirectory(dir);
                return;
            }
            catch (UnauthorizedAccessException)
            {
                retries++;
                if (retries >= MaxRetries)
                {
                    throw new Exception(
                        $"Unable to access OneDrive path after {MaxRetries} attempts. " +
                        $"Ensure the path exists in OneDrive and sync is complete: {dir}");
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

    public void Initialize()
    {
        var filePath = GetFilePath();
        bool isOneDrive = IsPathOneDrive(filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                EnsureDirectoryExistsWithRetry(filePath, isOneDrive);
                SaveKeys(new List<OKey>());
            }
        }
        catch (Exception ex)
        {
            // If OneDrive initialization fails, try setting up fallback
            if (isOneDrive)
            {
                try
                {
                    var fallbackPath = GetFallbackPath();
                    var fallbackDir = Path.GetDirectoryName(fallbackPath);
                    if (!string.IsNullOrEmpty(fallbackDir) && !Directory.Exists(fallbackDir))
                    {
                        Directory.CreateDirectory(fallbackDir);
                    }
                    _usingFallback = true;
                    _fallbackPath = fallbackPath;
                    
                    if (!File.Exists(fallbackPath))
                    {
                        SaveKeys(new List<OKey>());
                    }
                    
                    Console.Error.WriteLine(
                        $"Warning: OneDrive path {filePath} is not accessible. " +
                        $"Using local cache instead at {fallbackPath}. " +
                        $"Use 'config set-location' with a different path if you prefer.");
                    return;
                }
                catch (Exception fallbackEx)
                {
                    var errorMsg = $"Warning: Unable to initialize storage at OneDrive path: {filePath}. " +
                                   $"Also failed to set up local fallback: {fallbackEx.Message}";
                    Console.Error.WriteLine(errorMsg);
                    return;
                }
            }
            
            // Non-OneDrive error - just log warning
            var msg = $"Warning: Unable to initialize storage: {filePath}. {ex.Message}";
            Console.Error.WriteLine(msg);
        }
    }

    public List<OKey> GetKeys()
    {
        var filePath = GetFilePath();
        bool isOneDrive = IsPathOneDrive(filePath);
        
        try
        {
            if (!File.Exists(filePath))
            {
                // If OneDrive path doesn't exist, try fallback
                if (isOneDrive && !_usingFallback)
                {
                    var fallbackPath = GetFallbackPath();
                    if (File.Exists(fallbackPath))
                    {
                        _usingFallback = true;
                        _fallbackPath = fallbackPath;
                        return GetKeys(); // Recursively call to read from fallback
                    }
                }
                return new List<OKey>();
            }

            string content = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(content))
            {
                return new List<OKey>();
            }

            string json = _encryptionService.Decrypt(content);
            if (string.IsNullOrEmpty(json)) return new List<OKey>();
            
            return JsonSerializer.Deserialize(json, OpenerJsonContext.Default.ListOKey) ?? new List<OKey>();
        }
        catch (UnauthorizedAccessException ex)
        {
            // If reading from OneDrive fails, try fallback
            if (isOneDrive && !_usingFallback)
            {
                var fallbackPath = GetFallbackPath();
                if (File.Exists(fallbackPath))
                {
                    Console.Error.WriteLine(
                        $"Warning: Unable to read from OneDrive path. Using local cache instead: {fallbackPath}");
                    _usingFallback = true;
                    _fallbackPath = fallbackPath;
                    return GetKeys(); // Recursively call to read from fallback
                }
            }
            
            throw new Exception(
                $"Unable to read storage file at {filePath}. Windows denied access. " +
                $"If this is a OneDrive location and no local cache exists, the folder may need to be materialized. " +
                $"Try using a local storage path instead via 'config set-location'.", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error loading keys: {ex.Message}", ex);
        }
    }

    public void SaveKeys(List<OKey> keys)
    {
        var filePath = GetFilePath();
        bool isOneDrive = IsPathOneDrive(filePath);

        try
        {
            // Avoid touching OneDrive directories directly; Explorer-visible folders can still
            // reject CreateDirectory calls from .NET even when the path is valid.
            EnsureDirectoryExistsWithRetry(filePath, isOneDrive);

            string json = JsonSerializer.Serialize(keys, OpenerJsonContext.Default.ListOKey);
            string encrypted = _encryptionService.Encrypt(json);
            
            // For OneDrive, retry the file write operation
            if (isOneDrive)
            {
                Exception? lastException = null;
                for (int retries = 0; retries < MaxRetries; retries++)
                {
                    try
                    {
                        File.WriteAllText(filePath, encrypted);
                        if (_usingFallback)
                        {
                            _usingFallback = false; // Reset fallback flag on success
                        }
                        return;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        lastException = ex;
                        if (retries < MaxRetries - 1)
                        {
                            int delayMs = InitialRetryDelayMs + (retries * 300);
                            delayMs = Math.Min(delayMs, 2000);
                            Thread.Sleep(delayMs);
                        }
                    }
                }
                
                // OneDrive failed after retries - fall back to local cache
                Console.Error.WriteLine(
                    $"Warning: Unable to write to OneDrive path after {MaxRetries} attempts. " +
                    $"Using local cache instead: {GetFallbackPath()}");
                _usingFallback = true;
                _fallbackPath = GetFallbackPath();
                
                // Ensure fallback directory exists
                var fallbackDir = Path.GetDirectoryName(_fallbackPath);
                if (!string.IsNullOrEmpty(fallbackDir) && !Directory.Exists(fallbackDir))
                {
                    Directory.CreateDirectory(fallbackDir);
                }
                
                // Write to fallback location
                File.WriteAllText(_fallbackPath, encrypted);
                return;
            }
            
            File.WriteAllText(filePath, encrypted);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new Exception(
                $"Unable to write storage file at {filePath}. Windows denied access to the folder. " +
                $"If this path is under OneDrive, the sync engine may need additional configuration. " +
                $"Try using a local folder instead.", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error saving keys to {filePath}: {ex.Message}", ex);
        }
    }
}

