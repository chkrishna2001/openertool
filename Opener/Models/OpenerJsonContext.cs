using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Opener.Models;

[JsonSerializable(typeof(List<OKey>))]
[JsonSerializable(typeof(OKey))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class OpenerJsonContext : JsonSerializerContext
{
}
