using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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

    public void Initialize()
    {
        var filePath = GetFilePath();
        if (!File.Exists(filePath))
        {
            SaveKeys(new List<OKey>());
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
        string json = JsonSerializer.Serialize(keys, OpenerJsonContext.Default.ListOKey);
        string encrypted = _encryptionService.Encrypt(json);
        File.WriteAllText(filePath, encrypted);
    }
}

