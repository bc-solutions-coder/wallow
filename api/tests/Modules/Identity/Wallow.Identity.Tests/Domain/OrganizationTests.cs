using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Domain;

public class OrganizationTests
{
    private static readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());
    private static readonly Guid _testUserId = Guid.NewGuid();
    private static readonly Guid _memberUserId = Guid.NewGuid();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_WithValidParameters_ReturnsActiveOrganization()
    {
        Organization org = Organization.Create(
            _tenantId, "Acme Corp", "acme-corp", _testUserId, TimeProvider.System);

        org.TenantId.Should().Be(_tenantId);
        org.Name.Should().Be("Acme Corp");
        org.Slug.Should().Be("acme-corp");
        org.IsActive.Should().BeTrue();
        org.Members.Should().BeEmpty();
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
    public void AddMember_WithValidUser_AddsMemberToList()
    {
        Organization org = CreateOrganization();

        org.AddMember(_memberUserId, "member", _testUserId, TimeProvider.System);

        org.Members.Should().HaveCount(1);
        org.Members[0].UserId.Should().Be(_memberUserId);
        org.Members[0].Role.Should().Be("member");
    }

    [Fact]
    public void AddMember_DuplicateUser_ThrowsBusinessRuleException()
    {
        Organization org = CreateOrganization();
        org.AddMember(_memberUserId, "member", _testUserId, TimeProvider.System);

        Action act = () => org.AddMember(_memberUserId, "admin", _testUserId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*already a member*");
    }

    [Fact]
    public void RemoveMember_ExistingUser_RemovesMemberFromList()
    {
        Organization org = CreateOrganization();
        org.AddMember(_memberUserId, "member", _testUserId, TimeProvider.System);

        org.RemoveMember(_memberUserId, _testUserId, TimeProvider.System);

        org.Members.Should().BeEmpty();
    }

    [Fact]
    public void RemoveMember_NonExistentUser_ThrowsBusinessRuleException()
    {
        Organization org = CreateOrganization();

        Action act = () => org.RemoveMember(Guid.NewGuid(), _testUserId, TimeProvider.System);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*not a member*");
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
    public void AddMember_SetsAuditFields()
    {
        Organization org = Organization.Create(
            _tenantId, "Acme Corp", "acme-corp", _testUserId, _timeProvider);
        _timeProvider.Advance(TimeSpan.FromHours(1));

        org.AddMember(_memberUserId, "member", _testUserId, _timeProvider);

        org.UpdatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        org.UpdatedBy.Should().Be(_testUserId);
    }

    [Fact]
    public void RemoveMember_SetsAuditFields()
    {
        Organization org = Organization.Create(
            _tenantId, "Acme Corp", "acme-corp", _testUserId, _timeProvider);
        org.AddMember(_memberUserId, "member", _testUserId, _timeProvider);
        _timeProvider.Advance(TimeSpan.FromHours(1));

        org.RemoveMember(_memberUserId, _testUserId, _timeProvider);

        org.UpdatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
        org.UpdatedBy.Should().Be(_testUserId);
    }

    [Fact]
    public void AddMember_MultipleMembers_AllPresent()
    {
        Organization org = CreateOrganization();
        Guid secondMember = Guid.NewGuid();

        org.AddMember(_memberUserId, "member", _testUserId, TimeProvider.System);
        org.AddMember(secondMember, "admin", _testUserId, TimeProvider.System);

        org.Members.Should().HaveCount(2);
        org.Members.Should().Contain(m => m.UserId == _memberUserId);
        org.Members.Should().Contain(m => m.UserId == secondMember);
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

    private static Organization CreateOrganization() =>
        Organization.Create(_tenantId, "Acme Corp", "acme-corp", _testUserId, TimeProvider.System);
}
