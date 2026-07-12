using System.Text.Json;
using System.Text.RegularExpressions;

namespace Opener.Services;

/// <summary>
/// Pulls a single value out of a JSON document using a small dot-separated path syntax
/// (e.g. "access_token", "data.token", "items[0].id"). Deliberately not a full JSONPath
/// implementation - just enough to grab a field out of a typical API response, without an
/// external dependency (stays AOT-friendly, matches this project's other small static
/// "resolver" services like UrlTemplateResolver).
/// </summary>
public static class JsonPathExtractor
{
    private static readonly Regex ArraySegment = new(@"^([a-zA-Z_][a-zA-Z0-9_]*)\[(\d+)\]$", RegexOptions.Compiled);

    public static string? Extract(string json, string path)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var current = doc.RootElement;

            foreach (var segment in path.Split('.'))
            {
                var arrayMatch = ArraySegment.Match(segment);
                if (arrayMatch.Success)
                {
                    var propertyName = arrayMatch.Groups[1].Value;
                    var index = int.Parse(arrayMatch.Groups[2].Value);

                    if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(propertyName, out current))
                    {
                        return null;
                    }
                    if (current.ValueKind != JsonValueKind.Array || index >= current.GetArrayLength())
                    {
                        return null;
                    }
                    current = current[index];
                }
                else
                {
                    if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                    {
                        return null;
                    }
                }
            }

            return current.ValueKind switch
            {
                JsonValueKind.String => current.GetString(),
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                _ => current.GetRawText()
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
