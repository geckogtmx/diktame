
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
    /// Extracts a display name from the JWT. Tries <c>user_metadata.full_name</c>,
    /// then <c>user_metadata.name</c>, then falls back to the email prefix.
    /// </summary>
    public static string? ExtractDisplayName(string jwt)
    {
        var payload = DecodePayload(jwt);
        if (payload is null)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        // Try user_metadata.full_name → user_metadata.name
        if (root.TryGetProperty("user_metadata", out var meta))
        {
            if (meta.TryGetProperty("full_name", out var fn) && fn.ValueKind == JsonValueKind.String)
            {
                string name = fn.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }

            if (meta.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
            {
                string name = n.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }

        // Fall back to email prefix
        if (root.TryGetProperty("email", out var emailEl) && emailEl.ValueKind == JsonValueKind.String)
        {
            string email = emailEl.GetString() ?? "";
            int at = email.IndexOf('@', StringComparison.Ordinal);
            return at > 0 ? email[..at] : email;
        }

        return null;
    }

    /// <summary>
    /// Extracts the avatar URL from <c>user_metadata.avatar_url</c> (set by Google/GitHub OAuth).
    /// </summary>
    public static string? ExtractAvatarUrl(string jwt)
    {
        var payload = DecodePayload(jwt);
        if (payload is null)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("user_metadata", out var meta) &&
            meta.TryGetProperty("avatar_url", out var url) &&
            url.ValueKind == JsonValueKind.String)
        {
            return url.GetString();
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
