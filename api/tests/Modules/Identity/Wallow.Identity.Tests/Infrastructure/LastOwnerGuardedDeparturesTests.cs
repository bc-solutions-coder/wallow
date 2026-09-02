using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

/// <summary>
/// Every way an active membership can end runs its write INSIDE ILastOwnerGuard, not beside it.
/// A guard the write can outrun enforces nothing, so each case here hands the services a guard that
/// refuses and asserts the membership is untouched and nothing downstream was told it ended.
///
/// What the guard decides — and that two concurrent departures cannot both pass it — needs a real
/// Postgres row lock and lives in Wallow.Identity.IntegrationTests.
/// </summary>
public sealed class LastOwnerGuardedDeparturesTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();
    private readonly IAccessRevoker _accessRevoker = Substitute.For<IAccessRevoker>();
    private readonly ILastOwnerGuard _refusingGuard = Substitute.For<ILastOwnerGuard>();
    private readonly Organization _organization;
    private readonly Guid _orgId;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public LastOwnerGuardedDeparturesTests()
    {
        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new IdentityDbContext(options, DataProtectionProvider.Create("test"));

        _organization = Organization.Create(
            TenantId.Create(Guid.NewGuid()), "Acme", "acme", Guid.NewGuid(), TimeProvider.System);
        _orgId = _organization.Id.Value;
        _dbContext.SetTenant(_organization.TenantId);
        _dbContext.Organizations.Add(_organization);

        Membership owner = Membership.Enroll(
            _ownerId, _organization.Id, Guid.NewGuid(), TimeProvider.System);
        owner.MarkOwner(true, _actorId, TimeProvider.System);
        _dbContext.Memberships.Add(owner);
        _dbContext.SaveChanges();

        _refusingGuard.ExecuteDepartureAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new BusinessRuleException(IdentityErrors.LastOwner)));
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task LeaveAsync_WhenTheGuardRefuses_TakesNothingAway()
    {
        MembershipReviewService review = CreateReviewService();

        Func<Task> leave = () => review.LeaveAsync(_orgId, _ownerId);

        await leave.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "Identity.LastOwner");
        await AssertStillAnActiveMemberAsync();
    }

    [Fact]
    public async Task SuspendAsync_WhenTheGuardRefuses_TakesNothingAway()
    {
        MembershipReviewService review = CreateReviewService();

        Func<Task> suspend = () => review.SuspendAsync(_orgId, _ownerId, _actorId);

        await suspend.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "Identity.LastOwner");
        await AssertStillAnActiveMemberAsync();
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenTheGuardRefuses_TakesNothingAway()
    {
        IOrganizationRepository organizations = Substitute.For<IOrganizationRepository>();
        organizations.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns(_organization);
        OrganizationService organizationService = new(
            organizations,
            new MembershipRepository(_dbContext),
            _dbContext,
            _accessRevoker,
            Substitute.For<IOrganizationAdminEmailResolver>(),
            _refusingGuard,
            Substitute.For<IRegisteredClientRepository>(),
            Substitute.For<OpenIddict.Abstractions.IOpenIddictApplicationManager>(),
            _messageBus,
            Substitute.For<Wolverine.EntityFrameworkCore.IDbContextOutbox>(),
            TimeProvider.System,
            NullLogger<OrganizationService>.Instance);

        Func<Task> remove = () => organizationService.RemoveMemberAsync(_orgId, _ownerId, Guid.NewGuid());

        await remove.Should().ThrowAsync<BusinessRuleException>()
            .Where(e => e.Code == "Identity.LastOwner");
        await AssertStillAnActiveMemberAsync();
    }

    private MembershipReviewService CreateReviewService() => new(
        new MembershipRepository(_dbContext),
        _dbContext,
        Substitute.For<IDefaultMemberRoleResolver>(),
        _accessRevoker,
        _refusingGuard,
        _messageBus,
        TimeProvider.System,
        NullLogger<MembershipReviewService>.Instance);

    private async Task AssertStillAnActiveMemberAsync()
    {
        Membership? membership = await _dbContext.Memberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.UserId == _ownerId);

        membership.Should().NotBeNull();
        membership!.Status.Should().Be(MembershipStatus.Active);
        await _accessRevoker.DidNotReceive().RevokeMembershipAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<OrganizationMemberRemovedEvent>());
    }
}
