using Wallow.Identity.Application.Helpers;

namespace Wallow.Identity.Tests.Application;

public class ClientUriRulesTests
{
    [Theory]
    [InlineData("https://rp.example.com/backchannel-logout")]
    [InlineData("https://rp.example.com/bff/backchannel-logout?tenant=a")]
    public void TryParseBackchannelLogoutUri_AcceptsHttps_ForAnyClient(string value)
    {
        ClientUriRules.TryParseBackchannelLogoutUri(value, isConfidential: false, out Uri? uri)
            .Should().BeTrue();
        uri.Should().NotBeNull();
    }

    [Fact]
    public void TryParseBackchannelLogoutUri_AcceptsHttp_ForConfidentialClient()
    {
        ClientUriRules.TryParseBackchannelLogoutUri(
                "http://bff-example:3000/bff/backchannel-logout", isConfidential: true, out Uri? uri)
            .Should().BeTrue();
        uri!.Scheme.Should().Be("http");
    }

    [Fact]
    public void TryParseBackchannelLogoutUri_RefusesHttp_ForPublicClient()
    {
        ClientUriRules.TryParseBackchannelLogoutUri(
                "http://rp.example.com/backchannel-logout", isConfidential: false, out Uri? uri)
            .Should().BeFalse();
        uri.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/relative/path")]
    [InlineData("https://rp.example.com/logout#fragment")]
    [InlineData("ftp://rp.example.com/logout")]
    [InlineData("javascript:alert(1)")]
    public void TryParseBackchannelLogoutUri_RefusesMalformedValues(string? value)
    {
        ClientUriRules.TryParseBackchannelLogoutUri(value, isConfidential: true, out Uri? uri)
            .Should().BeFalse();
        uri.Should().BeNull();
    }

    [Fact]
    public void BackchannelLogoutUriError_NamesTheConfidentialRule()
    {
        ClientUriRules.BackchannelLogoutUriError.Should().Contain("confidential");
    }
}
