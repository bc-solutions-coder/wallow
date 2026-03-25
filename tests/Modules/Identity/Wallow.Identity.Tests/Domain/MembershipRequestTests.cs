using Microsoft.Extensions.Time.Testing;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Tests.Domain;

public class MembershipRequestTests
{
    private static readonly TenantId _tenantId = TenantId.Create(Guid.NewGuid());
    private static readonly Guid _userId = Guid.NewGuid();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_WithValidData_ReturnsPendingRequest()
    {
        MembershipRequest request = MembershipRequest.Create(
            _tenantId, _userId, "example.com", _userId, _timeProvider);

        request.TenantId.Should().Be(_tenantId);
        request.UserId.Should().Be(_userId);
        request.EmailDomain.Should().Be("example.com");
        request.Status.Should().Be(MembershipRequestStatus.Pending);
        request.ResolvedOrganizationId.Should().BeNull();
    }

    [Fact]
    public void Create_NormalizesEmailDomainToLowerCase()
    {
        MembershipRequest request = MembershipRequest.Create(
            _tenantId, _userId, "EXAMPLE.COM", _userId, _timeProvider);

        request.EmailDomain.Should().Be("example.com");
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsBusinessRuleException()
    {
        Action act = () => MembershipRequest.Create(
            _tenantId, Guid.Empty, "example.com", _userId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*User ID*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithBlankEmailDomain_ThrowsBusinessRuleException(string? emailDomain)
    {
        Action act = () => MembershipRequest.Create(
            _tenantId, _userId, emailDomain!, _userId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*Email domain*");
    }

    [Fact]
    public void Approve_WhenPending_SetsStatusToApproved()
    {
        MembershipRequest request = CreatePendingRequest();
        OrganizationId orgId = OrganizationId.New();

        request.Approve(orgId, _userId, _timeProvider);

        request.Status.Should().Be(MembershipRequestStatus.Approved);
        request.ResolvedOrganizationId.Should().Be(orgId);
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_ThrowsBusinessRuleException()
    {
        MembershipRequest request = CreatePendingRequest();
        request.Approve(OrganizationId.New(), _userId, _timeProvider);

        Action act = () => request.Approve(OrganizationId.New(), _userId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*not in a pending state*");
    }

    [Fact]
    public void Approve_WhenRejected_ThrowsBusinessRuleException()
    {
        MembershipRequest request = CreatePendingRequest();
        request.Reject(_userId, _timeProvider);

        Action act = () => request.Approve(OrganizationId.New(), _userId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*not in a pending state*");
    }

    [Fact]
    public void Reject_WhenPending_SetsStatusToRejected()
    {
        MembershipRequest request = CreatePendingRequest();

        request.Reject(_userId, _timeProvider);

        request.Status.Should().Be(MembershipRequestStatus.Rejected);
    }

    [Fact]
    public void Reject_WhenAlreadyApproved_ThrowsBusinessRuleException()
    {
        MembershipRequest request = CreatePendingRequest();
        request.Approve(OrganizationId.New(), _userId, _timeProvider);

        Action act = () => request.Reject(_userId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*not in a pending state*");
    }

    [Fact]
    public void Reject_WhenAlreadyRejected_ThrowsBusinessRuleException()
    {
        MembershipRequest request = CreatePendingRequest();
        request.Reject(_userId, _timeProvider);

        Action act = () => request.Reject(_userId, _timeProvider);

        act.Should().Throw<BusinessRuleException>()
            .WithMessage("*not in a pending state*");
    }

    private MembershipRequest CreatePendingRequest() =>
        MembershipRequest.Create(_tenantId, _userId, "example.com", _userId, _timeProvider);
}
