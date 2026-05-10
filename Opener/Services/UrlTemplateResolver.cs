using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Opener.Services;

public class UrlTemplateResolutionResult
{
    public string Value { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
}

public static class UrlTemplateResolver
{
    private static readonly Regex IndexedPlaceholderRegex = new(@"\{(\d+)(?:[^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex NamedPlaceholderRegex = new(@"<([a-zA-Z][a-zA-Z0-9_-]*)>", RegexOptions.Compiled);

    public static UrlTemplateResolutionResult Resolve(
        string template,
        string[]? args,
        Dictionary<string, Dictionary<string, string>>? globalAliases,
        Dictionary<string, string>? globalDefaults,
        Dictionary<string, Dictionary<string, string>>? keyAliases,
        Dictionary<string, string>? keyDefaults)
    {
        var result = new UrlTemplateResolutionResult { Value = template ?? string.Empty };
        if (string.IsNullOrEmpty(result.Value))
        {
            return result;
        }

        var normalizedArgs = args ?? Array.Empty<string>();
        var namedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positionalArgs = new List<string>();

        foreach (var token in normalizedArgs)
        {
            if (TryParseKeyValue(token, out var key, out var value))
            {
                namedArgs[key] = value;
                continue;
            }

            positionalArgs.Add(token);
        }

        var indexedOutcome = ResolveIndexedPlaceholders(result.Value, positionalArgs);
        result.Value = indexedOutcome.Value;
        result.Warnings.AddRange(indexedOutcome.Warnings);

        var mergedAliases = MergeAliases(globalAliases, keyAliases);
        var mergedDefaults = MergeDefaults(globalDefaults, keyDefaults);

        var remainingPositional = positionalArgs.Skip(indexedOutcome.ConsumedPositionalCount).ToList();
        var namedOutcome = ResolveNamedPlaceholders(result.Value, namedArgs, remainingPositional, mergedAliases, mergedDefaults);

        result.Value = namedOutcome.Value;
        result.Warnings.AddRange(namedOutcome.Warnings);

        return result;
    }

    private static (string Value, int ConsumedPositionalCount, List<string> Warnings) ResolveIndexedPlaceholders(string template, List<string> positionalArgs)
    {
        var warnings = new List<string>();
        var matches = IndexedPlaceholderRegex.Matches(template);
        if (matches.Count == 0)
        {
            return (template, 0, warnings);
        }

        var maxIndex = -1;
        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Groups[1].Value, out var idx))
            {
                continue;
            }

            if (idx > maxIndex)
            {
                maxIndex = idx;
            }
        }

        if (maxIndex < 0)
        {
            return (template, 0, warnings);
        }

        var formatArgs = new object?[maxIndex + 1];
        for (var i = 0; i <= maxIndex; i++)
        {
            formatArgs[i] = i < positionalArgs.Count ? positionalArgs[i] : "{" + i + "}";
        }

        try
        {
            var formatted = string.Format(template, formatArgs);
            var consumed = Math.Min(positionalArgs.Count, maxIndex + 1);
            if (positionalArgs.Count < maxIndex + 1)
            {
                warnings.Add("Some indexed placeholders were missing input values and were left unresolved.");
            }

            return (formatted, consumed, warnings);
        }
        catch (FormatException)
        {
            warnings.Add("Format string mismatch. Indexed placeholders were not fully resolved.");

            var replaced = IndexedPlaceholderRegex.Replace(template, match =>
            {
                var idx = int.Parse(match.Groups[1].Value);
                return idx < positionalArgs.Count ? positionalArgs[idx] : match.Value;
            });

            var consumed = Math.Min(positionalArgs.Count, maxIndex + 1);
            return (replaced, consumed, warnings);
        }
    }

    private static (string Value, List<string> Warnings) ResolveNamedPlaceholders(
        string template,
        Dictionary<string, string> namedArgs,
        List<string> positionalArgs,
        Dictionary<string, Dictionary<string, string>> aliases,
        Dictionary<string, string> defaults)
    {
        var warnings = new List<string>();
        var matches = NamedPlaceholderRegex.Matches(template);
        if (matches.Count == 0)
        {
            return (template, warnings);
        }

        var placeholderOrder = matches
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resolvedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positionalIndex = 0;

        foreach (var placeholder in placeholderOrder)
        {
            if (namedArgs.TryGetValue(placeholder, out var explicitValue))
            {
                resolvedValues[placeholder] = ResolveAlias(aliases, placeholder, explicitValue);
                continue;
            }

            if (positionalIndex < positionalArgs.Count)
            {
                var positionalValue = positionalArgs[positionalIndex++];
                resolvedValues[placeholder] = ResolveAlias(aliases, placeholder, positionalValue);
                continue;
            }

            if (defaults.TryGetValue(placeholder, out var defaultValue))
            {
                resolvedValues[placeholder] = ResolveAlias(aliases, placeholder, defaultValue);
                continue;
            }

            warnings.Add($"No value provided for <{placeholder}>. Keeping placeholder as-is.");
        }

        if (positionalIndex < positionalArgs.Count)
        {
            warnings.Add("Some positional arguments were unused.");
        }

        foreach (var kvp in resolvedValues)
        {
            var tokenRegex = new Regex($"<{Regex.Escape(kvp.Key)}>", RegexOptions.IgnoreCase);
            template = tokenRegex.Replace(template, kvp.Value ?? string.Empty);
        }

        return (template, warnings);
    }

    private static Dictionary<string, Dictionary<string, string>> MergeAliases(
        Dictionary<string, Dictionary<string, string>>? globalAliases,
        Dictionary<string, Dictionary<string, string>>? keyAliases)
    {
        var merged = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        if (globalAliases != null)
        {
            foreach (var entry in globalAliases)
            {
                merged[entry.Key] = CloneInnerMap(entry.Value);
            }
        }

        if (keyAliases != null)
        {
            foreach (var entry in keyAliases)
            {
                if (!merged.TryGetValue(entry.Key, out var inner))
                {
                    merged[entry.Key] = CloneInnerMap(entry.Value);
                }
                else
                {
                    // Merge inner maps: key-level entries override global entries, but preserve other global entries
                    foreach (var kv in entry.Value)
                    {
                        inner[kv.Key] = kv.Value;
                    }
                }
            }
        }

        return merged;
    }

    private static Dictionary<string, string> MergeDefaults(
        Dictionary<string, string>? globalDefaults,
        Dictionary<string, string>? keyDefaults)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (globalDefaults != null)
        {
            foreach (var entry in globalDefaults)
            {
                merged[entry.Key] = entry.Value;
            }
        }

        if (keyDefaults != null)
        {
            foreach (var entry in keyDefaults)
            {
                merged[entry.Key] = entry.Value;
            }
        }

        return merged;
    }

    private static Dictionary<string, string> CloneInnerMap(Dictionary<string, string>? source)
    {
        var clone = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (source == null)
        {
            return clone;
        }

        foreach (var kvp in source)
        {
            clone[kvp.Key] = kvp.Value;
        }

        return clone;
    }

    private static string ResolveAlias(
        Dictionary<string, Dictionary<string, string>> aliases,
        string placeholder,
        string value)
    {
        if (!aliases.TryGetValue(placeholder, out var aliasMap))
        {
            return value;
        }

        if (aliasMap.TryGetValue(value, out var aliasValue))
        {
            return aliasValue;
        }

        return value;
    }

    private static bool TryParseKeyValue(string token, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var sepIndex = token.IndexOf('=');
        if (sepIndex <= 0)
        {
            return false;
        }

        key = token.Substring(0, sepIndex).Trim();
        value = token.Substring(sepIndex + 1).Trim();

        return !string.IsNullOrWhiteSpace(key);
    }
}
