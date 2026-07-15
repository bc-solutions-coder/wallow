using Wallow.Auth.Helpers;

namespace Wallow.Auth.Component.Tests.Helpers;

public class ReturnUrlValidatorTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/login")]
    [InlineData("/connect/authorize?client_id=wallow-web&response_type=code")]
    [InlineData("/mfa/challenge?returnUrl=%2Fconnect%2Fauthorize")]
    [InlineData("/path/with spaces")]
    public void IsSafe_RelativePaths_ReturnsTrue(string url)
    {
        Assert.True(ReturnUrlValidator.IsSafe(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://evil.com")]
    [InlineData("http://evil.com")]
    [InlineData("HTTP://EVIL.COM")]
    [InlineData("//evil.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://evil.com/file")]
    [InlineData("evil.com/phish")]
    public void IsSafe_DangerousUrls_ReturnsFalse(string? url)
    {
        Assert.False(ReturnUrlValidator.IsSafe(url));
    }

    [Fact]
    public void Sanitize_SafeUrl_ReturnsUrl()
    {
        Assert.Equal("/dashboard", ReturnUrlValidator.Sanitize("/dashboard"));
    }

    [Fact]
    public void Sanitize_UnsafeUrl_ReturnsFallback()
    {
        Assert.Equal("/", ReturnUrlValidator.Sanitize("https://evil.com"));
    }

    [Fact]
    public void Sanitize_UnsafeUrl_ReturnsCustomFallback()
    {
        Assert.Equal("/login", ReturnUrlValidator.Sanitize("https://evil.com", "/login"));
    }

    [Fact]
    public void Sanitize_Null_ReturnsFallback()
    {
        Assert.Equal("/", ReturnUrlValidator.Sanitize(null));
    }
}
