using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Domain;

public class OrganizationTests
{
    private static readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());
    private static readonly Guid _testUserId = Guid.NewGuid();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_WithValidParameters_ReturnsActiveOrganization()
    {
        Organization org = Organization.Create(
            _tenantId, "Acme Corp", "acme-corp", _testUserId, TimeProvider.System);

        org.TenantId.Value.Should().Be(org.Id.Value);
        org.Name.Should().Be("Acme Corp");
        org.Slug.Should().Be("acme-corp");
        org.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_YieldsTenantIdEqualToId()
    {
        Organization org = Organization.Create(
            _tenantId, "Acme Corp", "acme-corp", _testUserId, TimeProvider.System);

        // Organization.Create is the only place a tenant id is minted, and it mints it from
        // the freshly generated org id.
        org.TenantId.Value.Should().Be(org.Id.Value);
    }

    [Fact]
    public void Create_MintsAFreshTenantIdPerOrganization()
    {
        Organization first = Organization.Create(
            _tenantId, "Acme Corp", "acme-corp", _testUserId, TimeProvider.System);
        Organization second = Organization.Create(
            _tenantId, "Beta Corp", "beta-corp", _testUserId, TimeProvider.System);

        // Each Create mints its own id/tenant equivalence; the caller-supplied value
        // does not determine the tenant id (Create is the single mint point).
        second.TenantId.Should().NotBe(first.TenantId);
        first.TenantId.Value.Should().Be(first.Id.Value);
        second.TenantId.Value.Should().Be(second.Id.Value);
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsBusinessRuleException()
    {
        Action act = () => Organization.Create(
            _tenantId, "", "acme-corp", _testUserId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void Create_WithEmptySlug_ThrowsBusinessRuleException()
    {
        Action act = () => Organization.Create(
            _tenantId, "Acme Corp", "", _testUserId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*slug*");
    }

    [Fact]
    public void Archive_WhenActive_SetsIsActiveToFalse()
    {
        Organization org = CreateOrganization();

        org.Archive(_testUserId, TimeProvider.System);

        org.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Archive_WhenAlreadyInactive_ThrowsBusinessRuleException()
    {
        Organization org = CreateOrganization();
        org.Archive(_testUserId, TimeProvider.System);

        Action act = () => org.Archive(_testUserId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*already inactive*");
    }

    [Fact]
    public void Reactivate_WhenInactive_SetsIsActiveToTrue()
    {
        Organization org = CreateOrganization();
        org.Archive(_testUserId, TimeProvider.System);

        org.Reactivate(_testUserId, TimeProvider.System);

        org.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Reactivate_WhenAlreadyActive_ThrowsBusinessRuleException()
    {
        Organization org = CreateOrganization();

        Action act = () => org.Reactivate(_testUserId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*already active*");
    }

    [Fact]
    public void Archive_SetsArchivedAtAndArchivedBy()
    {
        Organization org = CreateOrganization();

        org.Archive(_testUserId, TimeProvider.System);

        org.ArchivedAt.Should().NotBeNull();
        org.ArchivedBy.Should().Be(_testUserId);
    }

    [Fact]
    public void Reactivate_ClearsArchivedAtAndArchivedBy()
    {
        Organization org = CreateOrganization();
        org.Archive(_testUserId, TimeProvider.System);

        org.Reactivate(_testUserId, TimeProvider.System);

        org.ArchivedAt.Should().BeNull();
        org.ArchivedBy.Should().BeNull();
    }

    [Fact]
    public void ConfirmNameForDeletion_WithMatchingName_DoesNotThrow()
    {
        Organization org = CreateOrganization();

        Action act = () => Organization.ConfirmNameForDeletion(org, "Acme Corp");

        act.Should().NotThrow();
    }

    [Fact]
    public void ConfirmNameForDeletion_WithMismatchedName_ThrowsBusinessRuleException()
    {
        Organization org = CreateOrganization();

        Action act = () => Organization.ConfirmNameForDeletion(org, "Wrong Name");

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*does not match*");
    }

    [Fact]
    public void ConfirmNameForDeletion_IsCaseSensitive()
    {
        Organization org = CreateOrganization();

        Action act = () => Organization.ConfirmNameForDeletion(org, "acme corp");

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*does not match*");
    }

    [Fact]
    public void Create_WithValidParameters_SetsIdToNonDefault()
    {
        Organization org = CreateOrganization();

        org.Id.Should().NotBe(default);
    }

    [Fact]
    public void Create_SetsAuditFields()
    {
        Organization org = Organization.Create(
            _tenantId, "Acme Corp", "acme-corp", _testUserId, _timeProvider);

        org.CreatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        org.CreatedBy.Should().Be(_testUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_WithWhitespaceOrNullName_ThrowsBusinessRuleException(string? name)
    {
        Action act = () => Organization.Create(
            _tenantId, name!, "acme-corp", _testUserId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*name*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Create_WithWhitespaceOrNullSlug_ThrowsBusinessRuleException(string? slug)
    {
        Action act = () => Organization.Create(
            _tenantId, "Acme Corp", slug!, _testUserId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*slug*");
    }

    [Fact]
    public void Archive_SetsArchivedAtToTimeProviderTime()
    {
        Organization org = Organization.Create(
            _tenantId, "Acme Corp", "acme-corp", _testUserId, _timeProvider);
        _timeProvider.Advance(TimeSpan.FromDays(5));

        org.Archive(_testUserId, _timeProvider);

        org.ArchivedAt.Should().Be(_timeProvider.GetUtcNow());
    }

    [Fact]
    public void Archive_SetsUpdateAuditFields()
    {
        Organization org = Organization.Create(
            _tenantId, "Acme Corp", "acme-corp", _testUserId, _timeProvider);
        _timeProvider.Advance(TimeSpan.FromHours(2));

        org.Archive(_testUserId, _timeProvider);

        org.UpdatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        org.UpdatedBy.Should().Be(_testUserId);
    }

    [Fact]
    public void Reactivate_SetsUpdateAuditFields()
    {
        Organization org = Organization.Create(
            _tenantId, "Acme Corp", "acme-corp", _testUserId, _timeProvider);
        org.Archive(_testUserId, _timeProvider);
        _timeProvider.Advance(TimeSpan.FromHours(3));

        org.Reactivate(_testUserId, _timeProvider);

        org.UpdatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        org.UpdatedBy.Should().Be(_testUserId);
    }

    [Fact]
    public void SuspendByPlatform_RecordsReasonActorAndTime_AndLeavesTheOrgOwnStateAlone()
    {
        Organization org = Organization.Create(
            _tenantId, "Acme Corp", "acme-corp", _testUserId, _timeProvider);
        _timeProvider.Advance(TimeSpan.FromDays(2));

        org.SuspendByPlatform("Terms of service violation", _testUserId, _timeProvider);

        org.IsPlatformSuspended.Should().BeTrue();
        org.PlatformSuspensionReason.Should().Be("Terms of service violation");
        org.PlatformSuspendedBy.Should().Be(_testUserId);
        org.PlatformSuspendedAt.Should().Be(_timeProvider.GetUtcNow());
        // Platform suspension overrides the organization's own state; it does not rewrite it.
        org.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SuspendByPlatform_WhenAlreadySuspendedByPlatform_Throws()
    {
        Organization org = CreateOrganization();
        org.SuspendByPlatform("First reason", _testUserId, TimeProvider.System);

        Action act = () => org.SuspendByPlatform("Second reason", _testUserId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.OrganizationAlreadySuspendedByPlatform");
    }

    [Fact]
    public void SuspendByPlatform_WithoutAReason_Throws()
    {
        Organization org = CreateOrganization();

        Action act = () => org.SuspendByPlatform("   ", _testUserId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.PlatformSuspensionReasonRequired");
    }

    [Fact]
    public void ReinstateByPlatform_ClearsTheSuspension()
    {
        Organization org = CreateOrganization();
        org.SuspendByPlatform("Terms of service violation", _testUserId, TimeProvider.System);

        org.ReinstateByPlatform(_testUserId, TimeProvider.System);

        org.IsPlatformSuspended.Should().BeFalse();
        org.PlatformSuspensionReason.Should().BeNull();
        org.PlatformSuspendedBy.Should().BeNull();
        org.PlatformSuspendedAt.Should().BeNull();
    }

    [Fact]
    public void ReinstateByPlatform_WhenNotSuspendedByPlatform_Throws()
    {
        Organization org = CreateOrganization();

        Action act = () => org.ReinstateByPlatform(_testUserId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.OrganizationNotSuspendedByPlatform");
    }

    [Fact]
    public void EnsureDeletable_WhilePlatformSuspended_RefusesTheOrganizationItself()
    {
        Organization org = CreateOrganization();
        org.SuspendByPlatform("Under investigation", _testUserId, TimeProvider.System);

        Action act = () => org.EnsureDeletable(byPlatformOperator: false);

        act.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Identity.OrganizationSuspendedByPlatform");
    }

    [Fact]
    public void EnsureDeletable_WhilePlatformSuspended_AllowsThePlatformOperator()
    {
        Organization org = CreateOrganization();
        org.SuspendByPlatform("Under investigation", _testUserId, TimeProvider.System);

        Action act = () => org.EnsureDeletable(byPlatformOperator: true);

        act.Should().NotThrow("the operator who placed the freeze may also end the organization");
    }

    [Fact]
    public void EnsureDeletable_WhenNotPlatformSuspended_DoesNotThrow()
    {
        Organization org = CreateOrganization();
        org.Archive(_testUserId, TimeProvider.System);

        Action act = () => org.EnsureDeletable(byPlatformOperator: false);

        act.Should().NotThrow("archive is the organization's own state, not the platform's");
    }

    private static Organization CreateOrganization() =>
        Organization.Create(_tenantId, "Acme Corp", "acme-corp", _testUserId, TimeProvider.System);
}
