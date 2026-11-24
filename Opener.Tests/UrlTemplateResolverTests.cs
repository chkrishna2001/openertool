using System.Collections.Generic;
using Opener.Services;
using Xunit;

namespace Opener.Tests;

public class UrlTemplateResolverTests
{
    [Fact]
    public void Resolve_IndexedPlaceholder_RemainsBackwardCompatible()
    {
        var result = UrlTemplateResolver.Resolve(
            "https://jira.company.com/browse/{0}",
            new[] { "PROJ-123" },
            null,
            null,
            null,
            null);

        Assert.Equal("https://jira.company.com/browse/PROJ-123", result.Value);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Resolve_NamedPlaceholders_UsesPositionalAliasAndDefaults()
    {
        var aliases = new Dictionary<string, Dictionary<string, string>>
        {
            ["env"] = new Dictionary<string, string>
            {
                ["d"] = "-dev",
                ["u"] = "-uat",
                ["p"] = ""
            }
        };

        var defaults = new Dictionary<string, string>
        {
            ["user"] = "kchirravuri"
        };

        var result = UrlTemplateResolver.Resolve(
            "https://nvidia<env>.domain.com/<region>/<user>",
            new[] { "u", "us" },
            null,
            null,
            aliases,
            defaults);

        Assert.Equal("https://nvidia-uat.domain.com/us/kchirravuri", result.Value);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Resolve_NamedPlaceholders_KeyValueOverridesOrder()
    {
        var aliases = new Dictionary<string, Dictionary<string, string>>
        {
            ["env"] = new Dictionary<string, string>
            {
                ["d"] = "-dev",
                ["u"] = "-uat",
                ["p"] = ""
            }
        };

        var defaults = new Dictionary<string, string>
        {
            ["user"] = "kchirravuri"
        };

        var result = UrlTemplateResolver.Resolve(
            "https://nvidia<env>.domain.com/<region>/<user>",
            new[] { "region=us", "u" },
            null,
            null,
            aliases,
            defaults);

        Assert.Equal("https://nvidia-uat.domain.com/us/kchirravuri", result.Value);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Resolve_MixedIndexedAndNamed_ResolvesIndexedFirst()
    {
        var aliases = new Dictionary<string, Dictionary<string, string>>
        {
            ["env"] = new Dictionary<string, string>
            {
                ["d"] = "-dev",
                ["u"] = "-uat",
                ["p"] = ""
            }
        };

        var result = UrlTemplateResolver.Resolve(
            "https://api.domain.com/{0}/nvidia<env>",
            new[] { "service", "d" },
            null,
            null,
            aliases,
            null);

        Assert.Equal("https://api.domain.com/service/nvidia-dev", result.Value);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Resolve_ProdAlias_MapsToEmptySuffix()
    {
        var aliases = new Dictionary<string, Dictionary<string, string>>
        {
            ["env"] = new Dictionary<string, string>
            {
                ["d"] = "-dev",
                ["u"] = "-uat",
                ["p"] = ""
            }
        };

        var result = UrlTemplateResolver.Resolve(
            "https://nvidia<env>.domain.com",
            new[] { "p" },
            null,
            null,
            aliases,
            null);

        Assert.Equal("https://nvidia.domain.com", result.Value);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ResolveText_IndexedAndNamed_BothResolve()
    {
        var result = UrlTemplateResolver.ResolveText(
            "Hi <name>, ticket {0} is ready",
            new[] { "ABC-1", "name=Sam" },
            null, null, null, null);

        Assert.Equal("Hi Sam, ticket ABC-1 is ready", result.Value);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ResolveText_DoubleBraces_AreNotPlaceholders()
    {
        var result = UrlTemplateResolver.ResolveText(
            "{{0}} stays, {0} resolves, {{name}} stays",
            new[] { "X" },
            null, null, null, null);

        Assert.Equal("{{0}} stays, X resolves, {{name}} stays", result.Value);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ResolveText_StrayBracesAndJson_LeftUntouched()
    {
        // Resolve() runs string.Format, which throws on stray braces; ResolveText must not.
        var result = UrlTemplateResolver.ResolveText(
            "{\"id\": \"{0}\", \"fmt\": \"{0:x}\"} function() { return 1; }",
            new[] { "42" },
            null, null, null, null);

        Assert.Equal("{\"id\": \"42\", \"fmt\": \"{0:x}\"} function() { return 1; }", result.Value);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void ResolveText_UsesAliasesAndDefaults()
    {
        var aliases = new Dictionary<string, Dictionary<string, string>>
        {
            ["env"] = new Dictionary<string, string> { ["d"] = "dev" }
        };
        var defaults = new Dictionary<string, string> { ["region"] = "us" };

        var result = UrlTemplateResolver.ResolveText("deploy to <env>-<region>", new[] { "d" }, aliases, defaults, null, null);

        Assert.Equal("deploy to dev-us", result.Value);
    }

    [Fact]
    public void ResolveText_MissingValue_WarnsAndKeepsPlaceholder()
    {
        var result = UrlTemplateResolver.ResolveText("{0} and <name>", null, null, null, null, null);

        Assert.Equal("{0} and <name>", result.Value);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public void Resolve_NamedValueWithDollarSign_IsInsertedLiterally()
    {
        // As a replacement string, "$$" collapses to "$" and "$0" re-inserts the matched "<pw>".
        var result = UrlTemplateResolver.Resolve("https://x.com/<pw>", new[] { "pa$$word$0" }, null, null, null, null);

        Assert.Equal("https://x.com/pa$$word$0", result.Value);
    }
}
