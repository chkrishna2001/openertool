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
            "https://nexus<env>.bpc.com/<region>/<user>",
            new[] { "u", "us" },
            null,
            null,
            aliases,
            defaults);

        Assert.Equal("https://nexus-uat.bpc.com/us/kchirravuri", result.Value);
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
            "https://nexus<env>.bpc.com/<region>/<user>",
            new[] { "region=us", "u" },
            null,
            null,
            aliases,
            defaults);

        Assert.Equal("https://nexus-uat.bpc.com/us/kchirravuri", result.Value);
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
            "https://api.bpc.com/{0}/nexus<env>",
            new[] { "service", "d" },
            null,
            null,
            aliases,
            null);

        Assert.Equal("https://api.bpc.com/service/nexus-dev", result.Value);
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
            "https://nexus<env>.bpc.com",
            new[] { "p" },
            null,
            null,
            aliases,
            null);

        Assert.Equal("https://nexus.bpc.com", result.Value);
        Assert.Empty(result.Warnings);
    }
}
