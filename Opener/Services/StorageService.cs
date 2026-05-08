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

    public StorageService(IConfigService configService, IEncryptionService encryptionService)
    {
        _configService = configService;
        _encryptionService = encryptionService;
    }

    public string GetFilePath() => _configService.GetDataFilePath();

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
            // Log the error but allow the program to continue
            // This lets users recover by using 'config set-location' command
            var errorMsg = isOneDrive
                ? $"Warning: Unable to initialize storage at OneDrive path: {filePath}. {ex.Message}"
                : $"Warning: Unable to initialize storage: {filePath}. {ex.Message}";
            
            Console.Error.WriteLine(errorMsg);
            // Don't re-throw - let the program continue
        }
    }

    public List<OKey> GetKeys()
    {
        var filePath = GetFilePath();
        if (!File.Exists(filePath))
        {
            return new List<OKey>();
        }

        string content = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new List<OKey>();
        }

        try
        {
            string json = _encryptionService.Decrypt(content);
            if (string.IsNullOrEmpty(json)) return new List<OKey>();
            
            return JsonSerializer.Deserialize(json, OpenerJsonContext.Default.ListOKey) ?? new List<OKey>();
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
            // Ensure directory exists with retry logic for OneDrive
            EnsureDirectoryExistsWithRetry(filePath, isOneDrive);

            string json = JsonSerializer.Serialize(keys, OpenerJsonContext.Default.ListOKey);
            string encrypted = _encryptionService.Encrypt(json);
            
            // For OneDrive, retry the file write operation as well
            if (isOneDrive)
            {
                int retries = 0;
                int delayMs = InitialRetryDelayMs;

                while (retries < MaxRetries)
                {
                    try
                    {
                        File.WriteAllText(filePath, encrypted);
                        return;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        retries++;
                        if (retries >= MaxRetries)
                        {
                            throw new Exception(
                                $"Unable to write to OneDrive path after {MaxRetries} attempts: {filePath}");
                        }

                        Thread.Sleep(delayMs);
                        delayMs = Math.Min(delayMs + 300, 2000);
                    }
                }
            }
            else
            {
                File.WriteAllText(filePath, encrypted);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error saving keys to {filePath}: {ex.Message}", ex);
        }
    }
}

