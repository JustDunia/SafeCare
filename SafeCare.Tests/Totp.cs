using System.Security.Cryptography;

namespace SafeCare.Tests;

/// <summary>
/// Computes the code an authenticator app would show for a key (RFC 6238: SHA-1, 30-second
/// steps, six digits — what Identity's authenticator provider checks). Identity can verify
/// these codes but not produce them, so tests have to do it themselves.
/// </summary>
/// <remarks>
/// Shared with SafeCare.E2ETests as a linked file.
/// </remarks>
public static class Totp
{
    public static string Compute(string base32Key) => Compute(base32Key, DateTimeOffset.UtcNow);

    public static string Compute(string base32Key, DateTimeOffset time)
    {
        var key = FromBase32(base32Key);
        var timestep = time.ToUnixTimeSeconds() / 30;

        var counter = BitConverter.GetBytes(timestep);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];

        return (binary % 1_000_000).ToString("D6");
    }

    /// <summary>
    /// Accepts the key as Identity stores it or as the setup page shows it (lower case,
    /// in groups of four).
    /// </summary>
    private static byte[] FromBase32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = input.Replace(" ", string.Empty).TrimEnd('=').ToUpperInvariant();

        var output = new List<byte>(cleaned.Length * 5 / 8);
        int buffer = 0, bits = 0;

        foreach (var c in cleaned)
        {
            var value = alphabet.IndexOf(c);
            if (value < 0)
            {
                throw new FormatException($"'{c}' is not a base32 character.");
            }

            buffer = (buffer << 5) | value;
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)(buffer >> bits));
                buffer &= (1 << bits) - 1;
            }
        }

        return [.. output];
    }
}
