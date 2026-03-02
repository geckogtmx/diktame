
using System.Text;
using System.Text.Json;
using DiktaMe.Core.Account;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Account;

public sealed class JwtDecoderTests
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

    [Fact]
    public void ExtractEmail_ValidJwt_ReturnsEmail()
    {
        string jwt = BuildJwt(new { email = "test@example.com", sub = "user123" });

        string? email = JwtDecoder.ExtractEmail(jwt);

        email.Should().Be("test@example.com");
    }

    [Fact]
    public void ExtractEmail_MissingEmailClaim_ReturnsNull()
    {
        string jwt = BuildJwt(new { sub = "user123" });

        string? email = JwtDecoder.ExtractEmail(jwt);

        email.Should().BeNull();
    }

    [Fact]
    public void ExtractEmail_MalformedJwt_ReturnsNull()
    {
        string? email = JwtDecoder.ExtractEmail("not-a-jwt");

        email.Should().BeNull();
    }

    [Fact]
    public void ExtractEmail_EmptyString_ReturnsNull()
    {
        string? email = JwtDecoder.ExtractEmail("");

        email.Should().BeNull();
    }

    [Fact]
    public void ExtractExpiry_ValidJwt_ReturnsDateTimeOffset()
    {
        // 2025-01-01T00:00:00Z = 1735689600
        string jwt = BuildJwt(new { exp = 1735689600, email = "test@example.com" });

        DateTimeOffset? expiry = JwtDecoder.ExtractExpiry(jwt);

        expiry.Should().NotBeNull();
        expiry!.Value.Year.Should().Be(2025);
        expiry.Value.Month.Should().Be(1);
        expiry.Value.Day.Should().Be(1);
    }

    [Fact]
    public void ExtractExpiry_MissingExpClaim_ReturnsNull()
    {
        string jwt = BuildJwt(new { email = "test@example.com" });

        DateTimeOffset? expiry = JwtDecoder.ExtractExpiry(jwt);

        expiry.Should().BeNull();
    }

    [Fact]
    public void ExtractExpiry_MalformedJwt_ReturnsNull()
    {
        DateTimeOffset? expiry = JwtDecoder.ExtractExpiry("broken");

        expiry.Should().BeNull();
    }
}
