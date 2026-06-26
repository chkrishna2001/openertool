using System;
using System.Collections.Generic;
using System.IO;

namespace Opener.Commands;

public static class CommandHelpers
{
    public const string UrlAliasJsonHelp = "Inline JSON or a JSON file path. Shape: { \"placeholder\": { \"input\": \"replacement\" } }. Example file content: { \"env\": { \"d\": \"-dev\", \"u\": \"-uat\", \"p\": \"\" } }";
    public const string DefaultParamsJsonHelp = "Inline JSON or a JSON file path. Shape: { \"placeholder\": \"defaultValue\" }. Example file content: { \"user\": \"krishna\", \"region\": \"us\" }";

    public static bool TryParsePairs(string[] pairs, out Dictionary<string, string> parsed, out string error)
    {
        parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;

        foreach (var pair in pairs)
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0)
            {
                error = $"{pair}. Use input=replacement, for example d=-dev or p=.";
                return false;
            }

            var key = pair.Substring(0, separatorIndex).Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                error = $"{pair}. Alias input cannot be empty.";
                return false;
            }

            parsed[key] = pair.Substring(separatorIndex + 1);
        }

        return true;
    }

    public static string ResolveJsonInput(string? jsonOrFile)
    {
        if (string.IsNullOrWhiteSpace(jsonOrFile))
        {
            return string.Empty;
        }

        if (File.Exists(jsonOrFile))
        {
            return ReadJsonFile(jsonOrFile);
        }

        return jsonOrFile;
    }

    public static string ReadJsonFile(string path)
    {
        return File.ReadAllText(path);
    }
}
