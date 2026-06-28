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

        if (jsonOrFile == "-")
        {
            return Console.In.ReadToEnd();
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

    public static string NormalizeJson(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        var sb = new System.Text.StringBuilder();
        bool inDoubleQuote = false;
        bool inSingleQuote = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (c == '\\')
            {
                if (i + 1 < input.Length)
                {
                    char next = input[i + 1];
                    if (inSingleQuote && next == '\'')
                    {
                        sb.Append('\'');
                        i++;
                    }
                    else if (inSingleQuote && next == '"')
                    {
                        sb.Append("\\\"");
                        i++;
                    }
                    else
                    {
                        sb.Append(c);
                        sb.Append(next);
                        i++;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                if (!inSingleQuote)
                {
                    inDoubleQuote = !inDoubleQuote;
                    sb.Append(c);
                }
                else
                {
                    sb.Append("\\\"");
                }
            }
            else if (c == '\'')
            {
                if (!inDoubleQuote)
                {
                    inSingleQuote = !inSingleQuote;
                    sb.Append('"');
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
