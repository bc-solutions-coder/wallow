using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class DomainAssignmentServiceTests
{
    private readonly IOrganizationDomainRepository _domainRepo;
    private readonly IMembershipRequestRepository _membershipRepo;
    private readonly IMessageBus _messageBus;
    private readonly DomainAssignmentService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();

    public DomainAssignmentServiceTests()
    {
        _domainRepo = Substitute.For<IOrganizationDomainRepository>();
        _membershipRepo = Substitute.For<IMembershipRequestRepository>();
        _messageBus = Substitute.For<IMessageBus>();
        TenantContext tc = new(); tc.SetTenant(new TenantId(_tenantId));
        _sut = new DomainAssignmentService(_domainRepo, _membershipRepo, _messageBus, tc, TimeProvider.System, NullLogger<DomainAssignmentService>.Instance);
    }

    [Fact]
    public async Task RegisterDomainAsync_PersistsAndPublishes()
    {
        Guid orgId = Guid.NewGuid();
        Guid result = await _sut.RegisterDomainAsync(orgId, "example.com");
        result.Should().NotBe(Guid.Empty);
        _domainRepo.Received(1).Add(Arg.Any<OrganizationDomain>());
        await _domainRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationDomainRegisteredEvent>(e => e.OrganizationId == orgId && e.Domain == "example.com"));
    }

    [Fact]
    public async Task VerifyDomainAsync_WithValidToken_Verifies()
    {
        OrganizationDomain domain = OrganizationDomain.Create(new TenantId(_tenantId), OrganizationId.New(), "v.com", "tok", Guid.Empty, TimeProvider.System);
        _domainRepo.GetByIdAsync(Arg.Any<OrganizationDomainId>(), Arg.Any<CancellationToken>()).Returns(domain);
        await _sut.VerifyDomainAsync(domain.Id.Value, "tok");
        domain.IsVerified.Should().BeTrue();
        await _messageBus.Received(1).PublishAsync(Arg.Any<OrganizationDomainVerifiedEvent>());
    }

    [Fact]
    public async Task VerifyDomainAsync_NotFound_Throws()
    {
        _domainRepo.GetByIdAsync(Arg.Any<OrganizationDomainId>(), Arg.Any<CancellationToken>()).Returns((OrganizationDomain?)null);
        Func<Task> act = () => _sut.VerifyDomainAsync(Guid.NewGuid(), "x");
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task VerifyDomainAsync_BadToken_ThrowsBusiness()
    {
        OrganizationDomain domain = OrganizationDomain.Create(new TenantId(_tenantId), OrganizationId.New(), "b.com", "correct", Guid.Empty, TimeProvider.System);
        _domainRepo.GetByIdAsync(Arg.Any<OrganizationDomainId>(), Arg.Any<CancellationToken>()).Returns(domain);
        Func<Task> act = () => _sut.VerifyDomainAsync(domain.Id.Value, "wrong");
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task RequestMembershipAsync_PersistsAndPublishes()
    {
        Guid uid = Guid.NewGuid();
        Guid result = await _sut.RequestMembershipAsync(uid, "ex.com");
        result.Should().NotBe(Guid.Empty);
        _membershipRepo.Received(1).Add(Arg.Any<MembershipRequest>());
        await _messageBus.Received(1).PublishAsync(Arg.Is<MembershipRequestCreatedEvent>(e => e.UserId == uid));
    }

    [Fact]
    public async Task ApproveMembershipAsync_WhenExists_Approves()
    {
        Guid uid = Guid.NewGuid();
        MembershipRequest req = MembershipRequest.Create(new TenantId(_tenantId), uid, "a.com", uid, TimeProvider.System);
        _membershipRepo.GetByIdAsync(Arg.Any<MembershipRequestId>(), Arg.Any<CancellationToken>()).Returns(req);
        Guid orgId = Guid.NewGuid();
        await _sut.ApproveMembershipRequestAsync(req.Id.Value, orgId);
        await _messageBus.Received(1).PublishAsync(Arg.Is<MembershipRequestApprovedEvent>(e => e.OrganizationId == orgId));
    }

    [Fact]
    public async Task ApproveMembershipAsync_NotFound_Throws()
    {
        _membershipRepo.GetByIdAsync(Arg.Any<MembershipRequestId>(), Arg.Any<CancellationToken>()).Returns((MembershipRequest?)null);
        Func<Task> act = () => _sut.ApproveMembershipRequestAsync(Guid.NewGuid(), Guid.NewGuid());
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task RejectMembershipAsync_WhenExists_Rejects()
    {
        Guid uid = Guid.NewGuid();
        MembershipRequest req = MembershipRequest.Create(new TenantId(_tenantId), uid, "r.com", uid, TimeProvider.System);
        _membershipRepo.GetByIdAsync(Arg.Any<MembershipRequestId>(), Arg.Any<CancellationToken>()).Returns(req);
        await _sut.RejectMembershipRequestAsync(req.Id.Value);
        await _membershipRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectMembershipAsync_NotFound_Throws()
    {
        _membershipRepo.GetByIdAsync(Arg.Any<MembershipRequestId>(), Arg.Any<CancellationToken>()).Returns((MembershipRequest?)null);
        Func<Task> act = () => _sut.RejectMembershipRequestAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
