using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Opener.Models;

public class OpenerConfig
{
    public string StorageLocation { get; set; } = string.Empty; // Empty = default AppData
    public string EncryptionMode { get; set; } = "local"; // "local" or "portable"
}

[JsonSerializable(typeof(OpenerConfig))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class ConfigJsonContext : JsonSerializerContext { }
