using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Opener.Models;

[JsonSerializable(typeof(List<OKey>))]
[JsonSerializable(typeof(OKey))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(EmailTemplateData))]
[JsonSerializable(typeof(CalendarEventData))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true)]
public partial class OpenerJsonContext : JsonSerializerContext
{
}
