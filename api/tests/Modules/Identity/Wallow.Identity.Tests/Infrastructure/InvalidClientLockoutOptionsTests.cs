using Wallow.Identity.Infrastructure.Options;

namespace Wallow.Identity.Tests.Infrastructure;

public class InvalidClientLockoutOptionsTests
{
    [Fact]
    public void SectionName_IsIdentityInvalidClientLockout()
    {
        InvalidClientLockoutOptions.SectionName.Should().Be("Identity:InvalidClientLockout");
    }

    [Fact]
    public void Defaults_FiveFailuresInFiveMinutes_LockForFiveMinutes()
    {
        InvalidClientLockoutOptions options = new();

        options.FailureThreshold.Should().Be(5);
        options.WindowMinutes.Should().Be(5);
        options.LockoutMinutes.Should().Be(5);
    }
}
