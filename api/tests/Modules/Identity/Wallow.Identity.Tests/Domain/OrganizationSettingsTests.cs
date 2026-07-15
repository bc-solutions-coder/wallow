using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Domain;

public class OrganizationSettingsTests
{
    private static readonly OrganizationId _orgId = OrganizationId.New();
    private static readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());
    private static readonly Guid _userId = Guid.NewGuid();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_WithValidData_SetsAllProperties()
    {
        OrganizationSettings settings = OrganizationSettings.Create(
            _orgId, _tenantId, true, false, 14, _userId, _timeProvider);

        settings.OrganizationId.Should().Be(_orgId);
        settings.TenantId.Should().Be(_tenantId);
        settings.RequireMfa.Should().BeTrue();
        settings.AllowPasswordlessLogin.Should().BeFalse();
        settings.MfaGracePeriodDays.Should().Be(14);
    }

    [Fact]
    public void Create_WithDefaults_SetsExpectedValues()
    {
        OrganizationSettings settings = OrganizationSettings.Create(
            _orgId, _tenantId, false, true, 0, _userId, _timeProvider);

        settings.RequireMfa.Should().BeFalse();
        settings.AllowPasswordlessLogin.Should().BeTrue();
        settings.MfaGracePeriodDays.Should().Be(0);
    }

    [Fact]
    public void Update_ChangesAllProperties()
    {
        OrganizationSettings settings = OrganizationSettings.Create(
            _orgId, _tenantId, false, false, 7, _userId, _timeProvider);

        settings.Update(true, true, 30, _userId, _timeProvider);

        settings.RequireMfa.Should().BeTrue();
        settings.AllowPasswordlessLogin.Should().BeTrue();
        settings.MfaGracePeriodDays.Should().Be(30);
    }

    [Fact]
    public void Update_CanToggleSettingsOff()
    {
        OrganizationSettings settings = OrganizationSettings.Create(
            _orgId, _tenantId, true, true, 30, _userId, _timeProvider);

        settings.Update(false, false, 0, _userId, _timeProvider);

        settings.RequireMfa.Should().BeFalse();
        settings.AllowPasswordlessLogin.Should().BeFalse();
        settings.MfaGracePeriodDays.Should().Be(0);
    }

    [Fact]
    public void Create_GeneratesUniqueId()
    {
        OrganizationSettings settings1 = OrganizationSettings.Create(
            _orgId, _tenantId, false, false, 0, _userId, _timeProvider);
        OrganizationSettings settings2 = OrganizationSettings.Create(
            _orgId, _tenantId, false, false, 0, _userId, _timeProvider);

        settings1.Id.Should().NotBe(settings2.Id);
    }

    [Fact]
    public void Create_SetsCreatedAtAndCreatedBy()
    {
        OrganizationSettings settings = OrganizationSettings.Create(
            _orgId, _tenantId, true, false, 14, _userId, _timeProvider);

        settings.CreatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        settings.CreatedBy.Should().Be(_userId);
        settings.UpdatedAt.Should().BeNull();
        settings.UpdatedBy.Should().BeNull();
    }

    [Fact]
    public void Update_SetsUpdatedAtAndUpdatedBy()
    {
        OrganizationSettings settings = OrganizationSettings.Create(
            _orgId, _tenantId, false, false, 7, _userId, _timeProvider);

        Guid updaterUserId = Guid.NewGuid();
        _timeProvider.Advance(TimeSpan.FromHours(1));

        settings.Update(true, true, 30, updaterUserId, _timeProvider);

        settings.UpdatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        settings.UpdatedBy.Should().Be(updaterUserId);
        settings.CreatedAt.Should().BeBefore(settings.UpdatedAt!.Value);
    }

    [Fact]
    public void Update_WithNegativeGracePeriod_SetsValue()
    {
        OrganizationSettings settings = OrganizationSettings.Create(
            _orgId, _tenantId, false, false, 7, _userId, _timeProvider);

        settings.Update(false, false, -1, _userId, _timeProvider);

        settings.MfaGracePeriodDays.Should().Be(-1);
    }
}
