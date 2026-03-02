
using System.Text;
using System.Text.Json;

namespace DiktaMe.Core.Account;
/// <summary>
/// Lightweight JWT payload decoder. Extracts claims from the base64url-encoded
/// middle segment without cryptographic verification (the server validates on every request).
/// </summary>
internal static class JwtDecoder
{
    /// <summary>
    /// Extracts the <c>email</c> claim from a JWT, or null if missing/malformed.
    /// </summary>
    public static string? ExtractEmail(string jwt)
    {
        var payload = DecodePayload(jwt);
        if (payload is null)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.TryGetProperty("email", out var emailEl) && emailEl.ValueKind == JsonValueKind.String
            ? emailEl.GetString()
            : null;
    }

    /// <summary>
    /// Extracts the <c>exp</c> claim from a JWT as a <see cref="DateTimeOffset"/>,
    /// or null if missing/malformed.
    /// </summary>
    public static DateTimeOffset? ExtractExpiry(string jwt)
    {
        var payload = DecodePayload(jwt);
        if (payload is null)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("exp", out var expEl) && expEl.TryGetInt64(out long epoch))
        {
            return DateTimeOffset.FromUnixTimeSeconds(epoch);
        }

        return null;
    }

    /// <summary>
    /// Decodes the base64url-encoded payload (middle segment) of a JWT.
    /// </summary>
    private static string? DecodePayload(string jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
        {
            return null;
        }

        var parts = jwt.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        try
        {
            string base64 = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            // Pad to multiple of 4
            int padding = (4 - (base64.Length % 4)) % 4;
            base64 += new string('=', padding);

            byte[] bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
