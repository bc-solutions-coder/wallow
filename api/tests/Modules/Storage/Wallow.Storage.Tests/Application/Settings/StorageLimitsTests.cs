using Wallow.Storage.Application.Settings;

namespace Wallow.Storage.Tests.Application.Settings;

public class StorageLimitsTests
{
    [Fact]
    public void Create_ConvertsMegabytesToBytes()
    {
        StorageLimits limits = StorageLimits.Create(50, "*", 1024);

        limits.MaxUploadSizeBytes.Should().Be(50L * 1024 * 1024);
        limits.QuotaBytes.Should().Be(1024L * 1024 * 1024);
    }

    [Fact]
    public void IsExtensionAllowed_WhenWildcard_AllowsAnything()
    {
        StorageLimits limits = StorageLimits.Create(50, "*", 1024);

        limits.IsExtensionAllowed(".pdf").Should().BeTrue();
        limits.IsExtensionAllowed("exe").Should().BeTrue();
        limits.IsExtensionAllowed(string.Empty).Should().BeTrue();
    }

    [Fact]
    public void IsExtensionAllowed_WhenListIsBlank_AllowsAnything()
    {
        StorageLimits limits = StorageLimits.Create(50, "  ", 1024);

        limits.IsExtensionAllowed(".anything").Should().BeTrue();
    }

    [Fact]
    public void IsExtensionAllowed_MatchesCaseInsensitivelyAndIgnoresDots()
    {
        StorageLimits limits = StorageLimits.Create(50, "JPG, .png", 1024);

        limits.IsExtensionAllowed(".jpg").Should().BeTrue();
        limits.IsExtensionAllowed("PNG").Should().BeTrue();
    }

    [Fact]
    public void IsExtensionAllowed_WhenNotInList_ReturnsFalse()
    {
        StorageLimits limits = StorageLimits.Create(50, "jpg,png", 1024);

        limits.IsExtensionAllowed(".exe").Should().BeFalse();
    }

    [Fact]
    public void IsExtensionAllowed_WhenListIsRestrictive_RejectsMissingExtension()
    {
        StorageLimits limits = StorageLimits.Create(50, "jpg,png", 1024);

        limits.IsExtensionAllowed(string.Empty).Should().BeFalse();
    }
}
