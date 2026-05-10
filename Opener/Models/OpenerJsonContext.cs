using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Opener.Models;

[JsonSerializable(typeof(List<OKey>))]
[JsonSerializable(typeof(OKey))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class OpenerJsonContext : JsonSerializerContext
{
}
