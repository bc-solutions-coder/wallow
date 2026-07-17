using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Wallow-vec7.8: the registration-time membership request. The public
/// POST /v1/identity/membership-requests endpoint stays [Authorize]; the anonymous registration
/// flow instead asks this service to enqueue the request for the user it just created, with the
/// domain derived server-side from that user's own address. An anonymous caller therefore can
/// never name the domain.
/// </summary>
public sealed class DomainAssignmentServiceRegistrationMembershipTests
{
    private readonly IOrganizationDomainRepository _domainRepo;
    private readonly IMembershipRequestRepository _membershipRepo;
    private readonly IMessageBus _messageBus;
    private readonly DomainAssignmentService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public DomainAssignmentServiceRegistrationMembershipTests()
    {
        _domainRepo = Substitute.For<IOrganizationDomainRepository>();
        _membershipRepo = Substitute.For<IMembershipRequestRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        TenantContext tc = new();
        tc.SetTenant(new TenantId(_tenantId));
        _sut = new DomainAssignmentService(
            _domainRepo, _membershipRepo, _messageBus, tc, TimeProvider.System,
            NullLogger<DomainAssignmentService>.Instance);
    }

    /// <summary>Creates a domain row, verified or not, for the repository stub to return.</summary>
    private OrganizationDomain GivenDomain(string domain, bool verified)
    {
        OrganizationDomain orgDomain = OrganizationDomain.Create(
            new TenantId(_tenantId), OrganizationId.New(), domain, "tok", Guid.Empty, TimeProvider.System);

        if (verified)
        {
            orgDomain.Verify(Guid.Empty, TimeProvider.System);
        }

        _domainRepo.GetByDomainAsync(domain, Arg.Any<CancellationToken>()).Returns(orgDomain);
        return orgDomain;
    }

    [Fact]
    public async Task RequestMembershipForRegistrationAsync_WithVerifiedMatchingDomain_ReturnsRequestId()
    {
        GivenDomain("example.com", verified: true);

        Guid? result = await _sut.RequestMembershipForRegistrationAsync(_userId, "new@example.com");

        result.Should().NotBeNull();
        result.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task RequestMembershipForRegistrationAsync_WithVerifiedMatchingDomain_PersistsRequestForThatUser()
    {
        GivenDomain("example.com", verified: true);

        await _sut.RequestMembershipForRegistrationAsync(_userId, "new@example.com");

        _membershipRepo.Received(1).Add(Arg.Is<MembershipRequest>(r =>
            r.UserId == _userId && r.EmailDomain == "example.com"));
        await _membershipRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestMembershipForRegistrationAsync_WithVerifiedMatchingDomain_PublishesCreatedEvent()
    {
        GivenDomain("example.com", verified: true);

        await _sut.RequestMembershipForRegistrationAsync(_userId, "new@example.com");

        await _messageBus.Received(1).PublishAsync(Arg.Is<MembershipRequestCreatedEvent>(e =>
            e.UserId == _userId && e.EmailDomain == "example.com" && e.TenantId == _tenantId));
    }

    /// <summary>
    /// The domain is taken from the address, never from a caller-supplied string, and is matched
    /// case-insensitively the way the anonymous /organization-domains/match endpoint does it.
    /// </summary>
    [Fact]
    public async Task RequestMembershipForRegistrationAsync_DerivesLowercasedDomainFromEmail()
    {
        GivenDomain("example.com", verified: true);

        Guid? result = await _sut.RequestMembershipForRegistrationAsync(_userId, "New.User@EXAMPLE.COM");

        result.Should().NotBeNull();
        await _domainRepo.Received(1).GetByDomainAsync("example.com", Arg.Any<CancellationToken>());
        _membershipRepo.Received(1).Add(Arg.Is<MembershipRequest>(r => r.EmailDomain == "example.com"));
    }

    [Fact]
    public async Task RequestMembershipForRegistrationAsync_WithNoMatchingDomain_ReturnsNullAndEnqueuesNothing()
    {
        _domainRepo.GetByDomainAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((OrganizationDomain?)null);

        Guid? result = await _sut.RequestMembershipForRegistrationAsync(_userId, "new@nobody.com");

        result.Should().BeNull();
        _membershipRepo.DidNotReceive().Add(Arg.Any<MembershipRequest>());
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<MembershipRequestCreatedEvent>());
    }

    /// <summary>
    /// An unverified domain is one anyone could have claimed, so it must not admit membership
    /// requests. This mirrors the match endpoint, which 404s on unverified domains.
    /// </summary>
    [Fact]
    public async Task RequestMembershipForRegistrationAsync_WithUnverifiedDomain_ReturnsNullAndEnqueuesNothing()
    {
        GivenDomain("example.com", verified: false);

        Guid? result = await _sut.RequestMembershipForRegistrationAsync(_userId, "new@example.com");

        result.Should().BeNull();
        _membershipRepo.DidNotReceive().Add(Arg.Any<MembershipRequest>());
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<MembershipRequestCreatedEvent>());
    }

    [Fact]
    public async Task RequestMembershipForRegistrationAsync_WithEmailWithoutDomainPart_ReturnsNullAndEnqueuesNothing()
    {
        Guid? result = await _sut.RequestMembershipForRegistrationAsync(_userId, "not-an-email");

        result.Should().BeNull();
        await _domainRepo.DidNotReceive().GetByDomainAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _membershipRepo.DidNotReceive().Add(Arg.Any<MembershipRequest>());
    }
}
