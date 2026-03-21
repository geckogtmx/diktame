
using System.Text;
using System.Text.Json;
using DiktaMe.Core.Account;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Account;

public sealed class JwtDecoderExtendedTests
{
    private static string BuildJwt(object payload)
    {
        string header = Base64UrlEncode("""{"alg":"HS256","typ":"JWT"}""");
        string body = Base64UrlEncode(JsonSerializer.Serialize(payload));
        string signature = Base64UrlEncode("fake-signature");
        return $"{header}.{body}.{signature}";
    }

    private static string Base64UrlEncode(string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    // ── ExtractDisplayName ───────────────────────────────────────────────

    [Fact]
    public void ExtractDisplayName_WithFullName_ReturnsFullName()
    {
        string jwt = BuildJwt(new
        {
            email = "john@example.com",
            user_metadata = new { full_name = "John Doe", name = "johnd" },
        });

        JwtDecoder.ExtractDisplayName(jwt).Should().Be("John Doe");
    }

    [Fact]
    public void ExtractDisplayName_WithNameOnly_ReturnsName()
    {
        string jwt = BuildJwt(new
        {
            email = "jane@example.com",
            user_metadata = new { name = "Jane Smith" },
        });

        JwtDecoder.ExtractDisplayName(jwt).Should().Be("Jane Smith");
    }

    [Fact]
    public void ExtractDisplayName_EmptyFullName_FallsBackToName()
    {
        string jwt = BuildJwt(new
        {
            email = "user@example.com",
            user_metadata = new { full_name = "", name = "username" },
        });

        JwtDecoder.ExtractDisplayName(jwt).Should().Be("username");
    }

    [Fact]
    public void ExtractDisplayName_NoMetadata_FallsBackToEmailPrefix()
    {
        string jwt = BuildJwt(new { email = "alice@company.com" });

        JwtDecoder.ExtractDisplayName(jwt).Should().Be("alice");
    }

    [Fact]
    public void ExtractDisplayName_NoEmailOrMetadata_ReturnsNull()
    {
        string jwt = BuildJwt(new { sub = "user-123" });

        JwtDecoder.ExtractDisplayName(jwt).Should().BeNull();
    }

    [Fact]
    public void ExtractDisplayName_MalformedJwt_ReturnsNull()
    {
        JwtDecoder.ExtractDisplayName("not-a-jwt").Should().BeNull();
    }

    // ── ExtractAvatarUrl ─────────────────────────────────────────────────

    [Fact]
    public void ExtractAvatarUrl_WithAvatarUrl_ReturnsUrl()
    {
        string jwt = BuildJwt(new
        {
            email = "user@example.com",
            user_metadata = new { avatar_url = "https://lh3.googleusercontent.com/a/photo" },
        });

        JwtDecoder.ExtractAvatarUrl(jwt).Should().Be("https://lh3.googleusercontent.com/a/photo");
    }

    [Fact]
    public void ExtractAvatarUrl_NoAvatarUrl_ReturnsNull()
    {
        string jwt = BuildJwt(new
        {
            email = "user@example.com",
            user_metadata = new { full_name = "User" },
        });

        JwtDecoder.ExtractAvatarUrl(jwt).Should().BeNull();
    }

    [Fact]
    public void ExtractAvatarUrl_NoMetadata_ReturnsNull()
    {
        string jwt = BuildJwt(new { email = "user@example.com" });

        JwtDecoder.ExtractAvatarUrl(jwt).Should().BeNull();
    }

    [Fact]
    public void ExtractAvatarUrl_MalformedJwt_ReturnsNull()
    {
        JwtDecoder.ExtractAvatarUrl("broken").Should().BeNull();
    }
}
