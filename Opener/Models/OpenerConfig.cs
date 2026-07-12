using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Opener.Models;

public class OpenerConfig
{
    public string StorageLocation { get; set; } = string.Empty; // Empty = default AppData
    public string EncryptionMode { get; set; } = "local"; // "local" or "portable"
    public Dictionary<string, Dictionary<string, string>> GlobalUrlAliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> GlobalDefaultParams { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? GitSyncRemote { get; set; } // e.g. git@github.com:user/opener-vault.git or https://...
    public bool AutoSyncEnabled { get; set; } = false;
}

[JsonSerializable(typeof(OpenerConfig))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
public partial class ConfigJsonContext : JsonSerializerContext { }
