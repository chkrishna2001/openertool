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

    public StorageService(IConfigService configService, IEncryptionService encryptionService)
    {
        _configService = configService;
        _encryptionService = encryptionService;
    }

    public string GetFilePath() => _configService.GetDataFilePath();

    /// <summary>
    /// Ensures a directory exists. For OneDrive paths, this is a no-op since
    /// the folder must already exist via cloud sync.
    /// </summary>
    private static void EnsureDirectoryExists(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir) || Directory.Exists(dir))
        {
            return;
        }

        Directory.CreateDirectory(dir);
    }

    public void Initialize()
    {
        var filePath = GetFilePath();

        try
        {
            if (!File.Exists(filePath))
            {
                EnsureDirectoryExists(filePath);
                SaveKeys(new List<OKey>());
            }
        }
        catch (Exception ex)
        {
            // Log warning but allow program to continue
            Console.Error.WriteLine(
                $"Warning: Unable to initialize storage at {filePath}. " +
                $"If using OneDrive, ensure the folder is fully synced and accessible. " +
                $"Error: {ex.Message}");
        }
    }

    public List<OKey> GetKeys()
    {
        var filePath = GetFilePath();
        try
        {
            if (!File.Exists(filePath))
            {
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
            throw new Exception(
                $"Unable to read storage file at {filePath}. Access denied. " +
                $"If this path is under OneDrive, the folder may not be fully synced. " +
                $"Move your storage off the OneDrive-synced folder ('o config set-location') and use 'o sync' (git-based) or manual backups instead.", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error loading keys: {ex.Message}", ex);
        }
    }

    public void SaveKeys(List<OKey> keys)
    {
        var filePath = GetFilePath();

        try
        {
            EnsureDirectoryExists(filePath);

            string json = JsonSerializer.Serialize(keys, OpenerJsonContext.Default.ListOKey);
            string encrypted = _encryptionService.Encrypt(json);
            
            File.WriteAllText(filePath, encrypted);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new Exception(
                $"Unable to write storage file at {filePath}. Access denied. " +
                $"If this path is under OneDrive, the folder may not be fully synced. " +
                $"Move your storage off the OneDrive-synced folder ('o config set-location') and use 'o sync' (git-based) or manual backups instead.", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error saving keys to {filePath}: {ex.Message}", ex);
        }
    }
}

