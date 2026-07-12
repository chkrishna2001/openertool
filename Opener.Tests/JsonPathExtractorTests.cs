using Opener.Services;
using Xunit;

namespace Opener.Tests;

public class JsonPathExtractorTests
{
    [Fact]
    public void Extract_FlatField_ReturnsValue()
    {
        var json = "{\"access_token\":\"abc123\"}";

        var result = JsonPathExtractor.Extract(json, "access_token");

        Assert.Equal("abc123", result);
    }

    [Fact]
    public void Extract_NestedField_ReturnsValue()
    {
        var json = "{\"data\":{\"token\":\"nested-value\"}}";

        var result = JsonPathExtractor.Extract(json, "data.token");

        Assert.Equal("nested-value", result);
    }

    [Fact]
    public void Extract_ArrayIndex_ReturnsValue()
    {
        var json = "{\"items\":[{\"id\":\"first\"},{\"id\":\"second\"}]}";

        var result = JsonPathExtractor.Extract(json, "items[1].id");

        Assert.Equal("second", result);
    }

    [Fact]
    public void Extract_MissingPath_ReturnsNull()
    {
        var json = "{\"data\":{\"token\":\"value\"}}";

        var result = JsonPathExtractor.Extract(json, "data.missing");

        Assert.Null(result);
    }

    [Fact]
    public void Extract_ArrayIndexOutOfRange_ReturnsNull()
    {
        var json = "{\"items\":[{\"id\":\"only-one\"}]}";

        var result = JsonPathExtractor.Extract(json, "items[5].id");

        Assert.Null(result);
    }

    [Fact]
    public void Extract_NonObjectRoot_ReturnsNull()
    {
        var json = "[1,2,3]";

        var result = JsonPathExtractor.Extract(json, "field");

        Assert.Null(result);
    }

    [Fact]
    public void Extract_InvalidJson_ReturnsNullInsteadOfThrowing()
    {
        var result = JsonPathExtractor.Extract("not json at all", "field");

        Assert.Null(result);
    }

    [Fact]
    public void Extract_NumericValue_ReturnsRawText()
    {
        var json = "{\"count\":42}";

        var result = JsonPathExtractor.Extract(json, "count");

        Assert.Equal("42", result);
    }

    [Fact]
    public void Extract_EmptyPath_ReturnsNull()
    {
        var result = JsonPathExtractor.Extract("{\"a\":\"b\"}", "");

        Assert.Null(result);
    }
}
