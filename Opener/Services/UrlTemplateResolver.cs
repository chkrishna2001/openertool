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

    // Exactly {N}, not part of {{N}} - free text can't go through string.Format, which
    // throws on any stray brace and collapses {{ / }} into single braces.
    private static readonly Regex StrictIndexedPlaceholderRegex = new(@"(?<!\{)\{(\d+)\}(?!\})", RegexOptions.Compiled);

    public static UrlTemplateResolutionResult Resolve(
        string template,
        string[]? args,
        Dictionary<string, Dictionary<string, string>>? globalAliases,
        Dictionary<string, string>? globalDefaults,
        Dictionary<string, Dictionary<string, string>>? keyAliases,
        Dictionary<string, string>? keyDefaults)
        => ResolveCore(template, args, globalAliases, globalDefaults, keyAliases, keyDefaults, strictIndexed: false);

    /// <summary>
    /// Same placeholder rules as <see cref="Resolve"/> ({0} positional, &lt;name&gt; named with
    /// aliases/defaults), but for free text (Template keys): only an exact {N} is a placeholder,
    /// so {{0}}, {0:x}, JSON, and code braces are all left exactly as written.
    /// </summary>
    public static UrlTemplateResolutionResult ResolveText(
        string template,
        string[]? args,
        Dictionary<string, Dictionary<string, string>>? globalAliases,
        Dictionary<string, string>? globalDefaults,
        Dictionary<string, Dictionary<string, string>>? keyAliases,
        Dictionary<string, string>? keyDefaults)
        => ResolveCore(template, args, globalAliases, globalDefaults, keyAliases, keyDefaults, strictIndexed: true);

    // One alternation so a single left-to-right scan sees every token in the original text.
    // {{...}} is tried first, so {{0}} is a double-brace token, never a {0} placeholder.
    private static readonly Regex SinglePassTokenRegex = new(
        @"\{\{(?<inner>[^{}]+)\}\}|(?<!\{)\{(?<index>\d+)\}(?!\})|<(?<name>[a-zA-Z][a-zA-Z0-9_-]*)>",
        RegexOptions.Compiled);

    /// <summary>
    /// Resolves every token of <paramref name="template"/> in one pass over the original text:
    /// {{inner}} via <paramref name="resolveDoubleBrace"/> (null keeps it literal), and, when
    /// <paramref name="placeholders"/> is true, exact {N} and &lt;name&gt; placeholders with the
    /// same arg/alias/default rules as <see cref="Resolve"/>. Inserted values are final: they are
    /// never rescanned, so a value containing {0}, &lt;x&gt; or {{key:x}} reaches the output as-is.
    /// </summary>
    public static UrlTemplateResolutionResult ResolveSinglePass(
        string? template,
        string[]? args,
        Dictionary<string, Dictionary<string, string>>? globalAliases,
        Dictionary<string, string>? globalDefaults,
        Dictionary<string, Dictionary<string, string>>? keyAliases,
        Dictionary<string, string>? keyDefaults,
        bool placeholders,
        Func<string, string?>? resolveDoubleBrace)
    {
        var result = new UrlTemplateResolutionResult { Value = template ?? string.Empty };
        if (string.IsNullOrEmpty(result.Value))
        {
            return result;
        }

        var matches = SinglePassTokenRegex.Matches(result.Value);
        if (matches.Count == 0)
        {
            return result;
        }

        var indexedValues = new Dictionary<int, string>();
        var namedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (placeholders)
        {
            var (namedArgs, positionalArgs) = SplitArgs(args);

            // Indexed {N} first: {N} takes positional arg N; the highest index decides how many are consumed.
            var maxIndex = -1;
            foreach (Match m in matches)
            {
                if (m.Groups["index"].Success && int.TryParse(m.Groups["index"].Value, out var idx))
                {
                    maxIndex = Math.Max(maxIndex, idx);
                    if (idx < positionalArgs.Count)
                    {
                        indexedValues[idx] = positionalArgs[idx];
                    }
                }
            }

            var consumed = 0;
            if (maxIndex >= 0)
            {
                consumed = Math.Min(positionalArgs.Count, maxIndex + 1);
                if (positionalArgs.Count < maxIndex + 1)
                {
                    result.Warnings.Add("Some indexed placeholders were missing input values and were left unresolved.");
                }
            }

            // Then <name> in order of first appearance: name=value, next unused positional, default.
            var placeholderOrder = matches
                .Where(m => m.Groups["name"].Success)
                .Select(m => m.Groups["name"].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (placeholderOrder.Count > 0)
            {
                var aliases = MergeAliases(globalAliases, keyAliases);
                var defaults = MergeDefaults(globalDefaults, keyDefaults);
                var positionalIndex = consumed;

                foreach (var placeholder in placeholderOrder)
                {
                    if (namedArgs.TryGetValue(placeholder, out var explicitValue))
                    {
                        namedValues[placeholder] = ResolveAlias(aliases, placeholder, explicitValue);
                    }
                    else if (positionalIndex < positionalArgs.Count)
                    {
                        namedValues[placeholder] = ResolveAlias(aliases, placeholder, positionalArgs[positionalIndex++]);
                    }
                    else if (defaults.TryGetValue(placeholder, out var defaultValue))
                    {
                        namedValues[placeholder] = ResolveAlias(aliases, placeholder, defaultValue);
                    }
                    else
                    {
                        result.Warnings.Add($"No value provided for <{placeholder}>. Keeping placeholder as-is.");
                    }
                }

                if (positionalIndex < positionalArgs.Count)
                {
                    result.Warnings.Add("Some positional arguments were unused.");
                }
            }
        }

        // Evaluator, not a replacement string: values are inserted verbatim ($1 etc. are not group references).
        result.Value = SinglePassTokenRegex.Replace(result.Value, m =>
        {
            if (m.Groups["inner"].Success)
            {
                return resolveDoubleBrace?.Invoke(m.Groups["inner"].Value) ?? m.Value;
            }

            if (!placeholders)
            {
                return m.Value;
            }

            if (m.Groups["index"].Success)
            {
                return int.TryParse(m.Groups["index"].Value, out var idx) && indexedValues.TryGetValue(idx, out var v) ? v : m.Value;
            }

            return namedValues.TryGetValue(m.Groups["name"].Value, out var named) ? named ?? string.Empty : m.Value;
        });

        return result;
    }

    private static (Dictionary<string, string> Named, List<string> Positional) SplitArgs(string[]? args)
    {
        var namedArgs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positionalArgs = new List<string>();

        foreach (var token in args ?? Array.Empty<string>())
        {
            if (TryParseKeyValue(token, out var key, out var value))
            {
                namedArgs[key] = value;
                continue;
            }

            positionalArgs.Add(token);
        }

        return (namedArgs, positionalArgs);
    }

    private static UrlTemplateResolutionResult ResolveCore(
        string template,
        string[]? args,
        Dictionary<string, Dictionary<string, string>>? globalAliases,
        Dictionary<string, string>? globalDefaults,
        Dictionary<string, Dictionary<string, string>>? keyAliases,
        Dictionary<string, string>? keyDefaults,
        bool strictIndexed)
    {
        var result = new UrlTemplateResolutionResult { Value = template ?? string.Empty };
        if (string.IsNullOrEmpty(result.Value))
        {
            return result;
        }

        var (namedArgs, positionalArgs) = SplitArgs(args);

        var indexedOutcome = strictIndexed
            ? ResolveIndexedPlaceholdersStrict(result.Value, positionalArgs)
            : ResolveIndexedPlaceholders(result.Value, positionalArgs);
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

    private static (string Value, int ConsumedPositionalCount, List<string> Warnings) ResolveIndexedPlaceholdersStrict(string template, List<string> positionalArgs)
    {
        var warnings = new List<string>();
        var matches = StrictIndexedPlaceholderRegex.Matches(template);
        if (matches.Count == 0)
        {
            return (template, 0, warnings);
        }

        var maxIndex = matches.Max(m => int.TryParse(m.Groups[1].Value, out var idx) ? idx : -1);
        if (maxIndex < 0)
        {
            return (template, 0, warnings);
        }

        var replaced = StrictIndexedPlaceholderRegex.Replace(template, match =>
            int.TryParse(match.Groups[1].Value, out var idx) && idx < positionalArgs.Count
                ? positionalArgs[idx]
                : match.Value);

        if (positionalArgs.Count < maxIndex + 1)
        {
            warnings.Add("Some indexed placeholders were missing input values and were left unresolved.");
        }

        return (replaced, Math.Min(positionalArgs.Count, maxIndex + 1), warnings);
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
            // Evaluator, not a replacement string: a value like "$100" must not be read as a $1 group reference.
            var replacement = kvp.Value ?? string.Empty;
            template = tokenRegex.Replace(template, _ => replacement);
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
