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
}
