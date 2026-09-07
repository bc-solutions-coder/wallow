using Wallow.Notifications.Infrastructure.Services;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public class PushSettingsTests
{
    [Fact]
    public void PushSettings_DefaultConstructor_InitializesAllProviderDefaults()
    {
        PushSettings settings = new();

        settings.Fcm.Should().NotBeNull();
        settings.Apns.Should().NotBeNull();
    }

    [Fact]
    public void PushSettings_Fcm_CanBeSetAndRetrieved()
    {
        FcmDefaults fcm = new() { ServerKey = "key-123", ProjectId = "proj-456" };
        PushSettings settings = new() { Fcm = fcm };

        settings.Fcm.ServerKey.Should().Be("key-123");
        settings.Fcm.ProjectId.Should().Be("proj-456");
    }

    [Fact]
    public void PushSettings_Apns_CanBeSetAndRetrieved()
    {
        ApnsDefaults apns = new() { TeamId = "TEAM1", KeyId = "KEY1", BundleId = "com.test", UseSandbox = false };
        PushSettings settings = new() { Apns = apns };

        settings.Apns.TeamId.Should().Be("TEAM1");
        settings.Apns.KeyId.Should().Be("KEY1");
        settings.Apns.BundleId.Should().Be("com.test");
        settings.Apns.UseSandbox.Should().BeFalse();
    }


    [Fact]
    public void FcmDefaults_DefaultValues_AreEmptyStrings()
    {
        FcmDefaults defaults = new();

        defaults.ServerKey.Should().BeEmpty();
        defaults.ProjectId.Should().BeEmpty();
    }

    [Fact]
    public void ApnsDefaults_DefaultValues_AreCorrect()
    {
        ApnsDefaults defaults = new();

        defaults.TeamId.Should().BeEmpty();
        defaults.KeyId.Should().BeEmpty();
        defaults.BundleId.Should().BeEmpty();
        defaults.UseSandbox.Should().BeTrue();
    }

}
