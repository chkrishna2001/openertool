using System;
using System.IO;
using System.Text.Json;
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

    public ConfigService()
    {
        _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".opener");
        _configPath = Path.Combine(_configDir, "config.json");
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

    public string GetDataFilePath()
    {
        var config = GetConfig();
        
        if (!string.IsNullOrEmpty(config.StorageLocation))
        {
            // Ensure directory exists
            var dir = Path.GetDirectoryName(config.StorageLocation);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return config.StorageLocation;
        }

        // Default location
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
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
