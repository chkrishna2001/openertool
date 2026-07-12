using System;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Opener.Services;

/// <summary>
/// RFC 6238 (TOTP) code generation, compatible with Google Authenticator/Authy and any
/// other standard authenticator app using the default SHA-1/6-digit/30-second parameters.
/// </summary>
public static class TotpService
{
    private const int DefaultDigits = 6;
    private const int DefaultPeriodSeconds = 30;

    public static string GenerateCode(string secret, int digits = DefaultDigits, int periodSeconds = DefaultPeriodSeconds)
        => GenerateCode(secret, DateTimeOffset.UtcNow, digits, periodSeconds);

    /// <summary>
    /// Overload accepting an explicit timestamp, primarily so behavior can be verified
    /// against the RFC 6238 published test vectors instead of only self-consistency.
    /// </summary>
    public static string GenerateCode(string secret, DateTimeOffset atTime, int digits = DefaultDigits, int periodSeconds = DefaultPeriodSeconds)
    {
        var key = Base32.Decode(ExtractSecret(secret));
        long counter = atTime.ToUnixTimeSeconds() / periodSeconds;
        return ComputeCode(key, counter, digits);
    }

    public static TimeSpan TimeRemaining(int periodSeconds = DefaultPeriodSeconds)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long remaining = periodSeconds - (now % periodSeconds);
        return TimeSpan.FromSeconds(remaining);
    }

    /// <summary>
    /// Accepts either a raw base32 secret or a full otpauth:// URI (as exported by most
    /// 2FA setup flows) and returns just the base32 secret.
    /// </summary>
    public static string ExtractSecret(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var match = Regex.Match(input, @"[?&]secret=([^&]+)", RegexOptions.IgnoreCase);
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : input.Trim();
    }

    private static string ComputeCode(byte[] key, long counter, int digits)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);

        int offset = hash[^1] & 0x0F;
        int binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        int code = binaryCode % (int)Math.Pow(10, digits);
        return code.ToString(new string('0', digits));
    }
}

internal static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static byte[] Decode(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new FormatException("TOTP secret is empty.");
        }

        var cleaned = input.Trim().TrimEnd('=').Replace(" ", string.Empty).ToUpperInvariant();

        var bits = new System.Collections.Generic.List<byte>();
        int buffer = 0;
        int bitsLeft = 0;

        foreach (var c in cleaned)
        {
            int value = Alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new FormatException($"'{c}' is not a valid base32 character in the TOTP secret.");
            }

            buffer = (buffer << 5) | value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bits.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return bits.ToArray();
    }
}
