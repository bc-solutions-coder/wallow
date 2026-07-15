using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Domain;

public class ScimConfigurationTests
{
    private static readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());
    private static readonly Guid _testUserId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_CreatesDisabledConfiguration()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId, TimeProvider.System);

        config.TenantId.Should().Be(_tenantId);
        config.IsEnabled.Should().BeFalse();
        config.BearerToken.Should().NotBeNullOrWhiteSpace();
        config.TokenPrefix.Should().HaveLength(8);
        config.TokenExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddYears(1), TimeSpan.FromSeconds(5));
        config.AutoActivateUsers.Should().BeTrue();
        config.DeprovisionOnDelete.Should().BeFalse();
        config.LastSyncAt.Should().BeNull();
        config.DefaultRole.Should().BeNull();
    }

    [Fact]
    public void Enable_WhenDisabled_SetsIsEnabledToTrue()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId, TimeProvider.System);

        config.Enable(_testUserId, TimeProvider.System);

        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_ThrowsBusinessRuleException()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId, TimeProvider.System);
        config.Enable(_testUserId, TimeProvider.System);

        Action act = () => config.Enable(_testUserId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*already enabled*");
    }

    [Fact]
    public void Disable_WhenEnabled_SetsIsEnabledToFalse()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId, TimeProvider.System);
        config.Enable(_testUserId, TimeProvider.System);

        config.Disable(_testUserId, TimeProvider.System);

        config.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_ThrowsBusinessRuleException()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId, TimeProvider.System);

        Action act = () => config.Disable(_testUserId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*already disabled*");
    }

    [Fact]
    public void RegenerateToken_ReturnsNewTokenAndUpdatesFields()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId, TimeProvider.System);
        string originalToken = config.BearerToken;
        string originalPrefix = config.TokenPrefix;

        string plainTextToken = config.RegenerateToken(_testUserId, TimeProvider.System);

        plainTextToken.Should().NotBeNullOrWhiteSpace();
        config.BearerToken.Should().NotBe(originalToken);
        config.TokenPrefix.Should().NotBe(originalPrefix);
        config.TokenExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddYears(1), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateSettings_UpdatesAllSettingFields()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId, TimeProvider.System);

        config.UpdateSettings(false, "admin", true, _testUserId, TimeProvider.System);

        config.AutoActivateUsers.Should().BeFalse();
        config.DefaultRole.Should().Be("admin");
        config.DeprovisionOnDelete.Should().BeTrue();
    }

    [Fact]
    public void RecordSync_SetsLastSyncAtToCurrentTime()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId, TimeProvider.System);
        DateTime beforeSync = DateTime.UtcNow;

        config.RecordSync(_testUserId, TimeProvider.System);

        config.LastSyncAt.Should().NotBeNull();
        config.LastSyncAt.Should().BeOnOrAfter(beforeSync);
        config.LastSyncAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void IsTokenValid_WhenEnabledAndNotExpired_ReturnsTrue()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId, TimeProvider.System);
        config.Enable(_testUserId, TimeProvider.System);

        bool isValid = config.IsTokenValid(TimeProvider.System);

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsTokenValid_WhenDisabled_ReturnsFalse()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId, TimeProvider.System);

        bool isValid = config.IsTokenValid(TimeProvider.System);

        isValid.Should().BeFalse();
    }
}
