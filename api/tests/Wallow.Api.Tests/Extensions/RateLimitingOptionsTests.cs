using Wallow.Api.Extensions;

namespace Wallow.Api.Tests.Extensions;

public sealed class RateLimitingOptionsTests
{
    [Fact]
    public void SectionName_IsRateLimiting()
    {
        RateLimitingOptions.SectionName.Should().Be("RateLimiting");
    }

    [Fact]
    public void Auth_DefaultsToRateLimitDefaults()
    {
        RateLimitingOptions options = new();

        options.Auth.PermitLimit.Should().Be(RateLimitDefaults.AuthPermitLimit);
        options.Auth.WindowMinutes.Should().Be(RateLimitDefaults.AuthWindowMinutes);
    }

    [Fact]
    public void Upload_DefaultsToRateLimitDefaults()
    {
        RateLimitingOptions options = new();

        options.Upload.PermitLimit.Should().Be(RateLimitDefaults.UploadPermitLimit);
        options.Upload.WindowHours.Should().Be(RateLimitDefaults.UploadWindowHours);
    }

    [Fact]
    public void Registration_DefaultsToRateLimitDefaults()
    {
        RateLimitingOptions options = new();

        options.Registration.PermitLimit.Should().Be(RateLimitDefaults.RegistrationPermitLimit);
        options.Registration.WindowHours.Should().Be(RateLimitDefaults.RegistrationWindowHours);
    }

    [Fact]
    public void Global_DefaultsToRateLimitDefaults()
    {
        RateLimitingOptions options = new();

        options.Global.PermitLimit.Should().Be(RateLimitDefaults.GlobalPermitLimit);
        options.Global.WindowHours.Should().Be(RateLimitDefaults.GlobalWindowHours);
    }
}
