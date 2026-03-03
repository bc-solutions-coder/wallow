using Foundry.Identity.Domain.Entities;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;

namespace Foundry.Identity.Tests.Domain;

public class ScimConfigurationTests
{
    private static readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());
    private static readonly Guid _testUserId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidParameters_CreatesDisabledConfiguration()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId);

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
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId);

        config.Enable(_testUserId);

        config.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_ThrowsBusinessRuleException()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId);
        config.Enable(_testUserId);

        Action act = () => config.Enable(_testUserId);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*already enabled*");
    }

    [Fact]
    public void Disable_WhenEnabled_SetsIsEnabledToFalse()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId);
        config.Enable(_testUserId);

        config.Disable(_testUserId);

        config.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_ThrowsBusinessRuleException()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId);

        Action act = () => config.Disable(_testUserId);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*already disabled*");
    }

    [Fact]
    public void RegenerateToken_ReturnsNewTokenAndUpdatesFields()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId);
        string originalToken = config.BearerToken;
        string originalPrefix = config.TokenPrefix;

        string plainTextToken = config.RegenerateToken(_testUserId);

        plainTextToken.Should().NotBeNullOrWhiteSpace();
        config.BearerToken.Should().NotBe(originalToken);
        config.TokenPrefix.Should().NotBe(originalPrefix);
        config.TokenExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddYears(1), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateSettings_UpdatesAllSettingFields()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId);

        config.UpdateSettings(false, "admin", true, _testUserId);

        config.AutoActivateUsers.Should().BeFalse();
        config.DefaultRole.Should().Be("admin");
        config.DeprovisionOnDelete.Should().BeTrue();
    }

    [Fact]
    public void RecordSync_SetsLastSyncAtToCurrentTime()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId);
        DateTime beforeSync = DateTime.UtcNow;

        config.RecordSync(_testUserId);

        config.LastSyncAt.Should().NotBeNull();
        config.LastSyncAt.Should().BeOnOrAfter(beforeSync);
        config.LastSyncAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void IsTokenValid_WhenEnabledAndNotExpired_ReturnsTrue()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId);
        config.Enable(_testUserId);

        bool isValid = config.IsTokenValid();

        isValid.Should().BeTrue();
    }

    [Fact]
    public void IsTokenValid_WhenDisabled_ReturnsFalse()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, _testUserId);

        bool isValid = config.IsTokenValid();

        isValid.Should().BeFalse();
    }
}
