using Wallow.Api.Extensions;

namespace Wallow.Api.Tests.Middleware;

public sealed class RateLimitDefaultsTests
{
    [Fact]
    public void AuthPermitLimit_HasExpectedValue()
    {
        RateLimitDefaults.AuthPermitLimit.Should().Be(3);
    }

    [Fact]
    public void AuthWindowMinutes_HasExpectedValue()
    {
        RateLimitDefaults.AuthWindowMinutes.Should().Be(10);
    }

    [Fact]
    public void UploadPermitLimit_HasExpectedValue()
    {
        RateLimitDefaults.UploadPermitLimit.Should().Be(10);
    }

    [Fact]
    public void UploadWindowHours_HasExpectedValue()
    {
        RateLimitDefaults.UploadWindowHours.Should().Be(1);
    }

    [Fact]
    public void GlobalPermitLimit_HasExpectedValue()
    {
        RateLimitDefaults.GlobalPermitLimit.Should().Be(1000);
    }

    [Fact]
    public void GlobalWindowHours_HasExpectedValue()
    {
        RateLimitDefaults.GlobalWindowHours.Should().Be(1);
    }

    [Fact]
    public void AuthPermitLimit_IsMoreRestrictiveThanGlobal()
    {
        RateLimitDefaults.AuthPermitLimit.Should().BeLessThan(RateLimitDefaults.GlobalPermitLimit);
    }

    [Fact]
    public void UploadPermitLimit_IsMoreRestrictiveThanGlobal()
    {
        RateLimitDefaults.UploadPermitLimit.Should().BeLessThan(RateLimitDefaults.GlobalPermitLimit);
    }

    [Fact]
    public void AllPermitLimits_ArePositive()
    {
        RateLimitDefaults.AuthPermitLimit.Should().BePositive();
        RateLimitDefaults.UploadPermitLimit.Should().BePositive();
        RateLimitDefaults.GlobalPermitLimit.Should().BePositive();
    }

    [Fact]
    public void AllWindowDurations_ArePositive()
    {
        RateLimitDefaults.AuthWindowMinutes.Should().BePositive();
        RateLimitDefaults.UploadWindowHours.Should().BePositive();
        RateLimitDefaults.GlobalWindowHours.Should().BePositive();
    }
}
