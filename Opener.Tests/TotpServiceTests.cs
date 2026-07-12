using System;
using Opener.Services;
using Xunit;

namespace Opener.Tests;

public class TotpServiceTests
{
    // Base32 encoding of the RFC 6238 Appendix B published SHA-1 test seed
    // (the raw ASCII string "12345678901234567890"). RFC 6238's own vectors table
    // uses that raw seed as the HMAC key directly, so base32-encoding it first and
    // letting GenerateCode decode it back reproduces the exact same key bytes.
    private const string Rfc6238Sha1Secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Theory]
    [InlineData(59L, "94287082")]
    [InlineData(1111111109L, "07081804")]
    [InlineData(1111111111L, "14050471")]
    [InlineData(1234567890L, "89005924")]
    [InlineData(2000000000L, "69279037")]
    public void GenerateCode_MatchesRfc6238PublishedTestVectors(long unixSeconds, string expectedCode)
    {
        var atTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        var code = TotpService.GenerateCode(Rfc6238Sha1Secret, atTime, digits: 8, periodSeconds: 30);

        Assert.Equal(expectedCode, code);
    }

    [Fact]
    public void GenerateCode_DefaultParameters_ProducesSixDigitCode()
    {
        var code = TotpService.GenerateCode(Rfc6238Sha1Secret, DateTimeOffset.FromUnixTimeSeconds(59));

        Assert.Equal(6, code.Length);
        Assert.True(long.TryParse(code, out _));
    }

    [Fact]
    public void GenerateCode_SameTimeStep_IsDeterministic()
    {
        var atTime = DateTimeOffset.FromUnixTimeSeconds(1111111109);

        var first = TotpService.GenerateCode(Rfc6238Sha1Secret, atTime);
        var second = TotpService.GenerateCode(Rfc6238Sha1Secret, atTime);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GenerateCode_DifferentTimeSteps_ProduceDifferentCodes()
    {
        var first = TotpService.GenerateCode(Rfc6238Sha1Secret, DateTimeOffset.FromUnixTimeSeconds(59));
        var second = TotpService.GenerateCode(Rfc6238Sha1Secret, DateTimeOffset.FromUnixTimeSeconds(1111111109));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ExtractSecret_PlainBase32_ReturnedUnchanged()
    {
        var result = TotpService.ExtractSecret(Rfc6238Sha1Secret);

        Assert.Equal(Rfc6238Sha1Secret, result);
    }

    [Fact]
    public void ExtractSecret_OtpauthUri_ExtractsSecretParameter()
    {
        var uri = $"otpauth://totp/GitHub:me@example.com?secret={Rfc6238Sha1Secret}&issuer=GitHub";

        var result = TotpService.ExtractSecret(uri);

        Assert.Equal(Rfc6238Sha1Secret, result);
    }

    [Fact]
    public void GenerateCode_InvalidBase32Character_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => TotpService.GenerateCode("not-valid-base32!!!"));
    }

    [Fact]
    public void GenerateCode_EmptySecret_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => TotpService.GenerateCode(string.Empty));
    }
}
