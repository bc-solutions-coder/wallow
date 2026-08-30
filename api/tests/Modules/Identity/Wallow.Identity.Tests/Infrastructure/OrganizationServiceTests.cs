using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wolverine;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class OrganizationServiceTests : IDisposable
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly MembershipRepository _membershipRepository;
    private readonly IdentityDbContext _dbContext;
    private readonly IMessageBus _messageBus;
    private readonly OrganizationService _sut;
    private readonly IAccessRevoker _accessRevoker;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly TenantContext _tenantContextInstance;

    public OrganizationServiceTests()
    {
        _tenantContextInstance = new TenantContext();
        _tenantContextInstance.SetTenant(new TenantId(_tenantId));

        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Identity.Tests");
        _dbContext = new IdentityDbContext(options, dataProtectionProvider);
        _dbContext.SetTenant(new TenantId(_tenantId));

        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _membershipRepository = new MembershipRepository(_dbContext);
        _messageBus = Substitute.For<IMessageBus>();
        _accessRevoker = Substitute.For<IAccessRevoker>();

        SeedRoleCatalog();

        _sut = new OrganizationService(
            _organizationRepository,
            _membershipRepository,
            _dbContext,
            _accessRevoker,
            Substitute.For<IOrganizationAdminEmailResolver>(),
            new UnguardedLastOwnerGuard(),
            _messageBus,
            TimeProvider.System,
            NullLogger<OrganizationService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    // Roles are a global catalog addressed by name; the service resolves a name to an id before it
    // can grant anything, so an unseeded catalog fails every membership write.
    private void SeedRoleCatalog()
    {
        foreach (string roleName in new[] { "admin", "manager", "user" })
        {
            _dbContext.Roles.Add(new WallowRole
            {
                Id = Guid.NewGuid(),
                Name = roleName,
                NormalizedName = roleName.ToUpperInvariant()
            });
        }

        _dbContext.SaveChanges();
    }

    private Guid RoleId(string roleName)
    {
        string normalized = roleName.ToUpperInvariant();
        return _dbContext.Roles.IgnoreQueryFilters().Single(r => r.NormalizedName == normalized).Id;
    }

    private Task<Membership?> MembershipFor(Guid userId, Guid orgId)
        => _membershipRepository.GetAsync(userId, orgId);

    [Fact]
    public async Task CreateOrganizationAsync_WithValidName_CreatesAndPublishesEvent()
    {
        _organizationRepository
            .When(r => r.Add(Arg.Any<Organization>()))
            .Do(_ => { });

        Guid result = await _sut.CreateOrganizationAsync("Test Org", "test.com", "admin@test.com");

        result.Should().NotBe(Guid.Empty);
        _organizationRepository.Received(1).Add(Arg.Any<Organization>());
        await _organizationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationCreatedEvent>(e =>
            e.Name == "Test Org" &&
            e.Domain == "test.com" &&
            e.CreatorEmail == "admin@test.com" &&
            e.TenantId == result));
    }

    [Fact]
    public async Task CreateOrganizationAsync_WithNullCreatorEmail_UsesEmptyString()
    {
        Guid result = await _sut.CreateOrganizationAsync("Test Org");

        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationCreatedEvent>(e =>
            e.CreatorEmail == string.Empty));
    }

    [Fact]
    public async Task CreateOrganizationAsync_WithCreatorUserId_AddsCreatorAsAdminMemberAndStampsAuditFields()
    {
        Guid creatorUserId = Guid.NewGuid();
        Organization? capturedOrganization = null;
        _organizationRepository
            .When(r => r.Add(Arg.Any<Organization>()))
            .Do(call => capturedOrganization = call.Arg<Organization>());

        Guid result = await _sut.CreateOrganizationAsync("Test Org", creatorUserId: creatorUserId);

        result.Should().NotBe(Guid.Empty);
        capturedOrganization.Should().NotBeNull();
        capturedOrganization!.CreatedBy.Should().Be(creatorUserId);

        Membership? membership = await MembershipFor(creatorUserId, result);
        membership.Should().NotBeNull();
        membership!.IsActive.Should().BeTrue();
        membership.IsOwner.Should().BeTrue();
        membership.RoleIds.Should().BeEquivalentTo([RoleId("admin")]);
    }

    /// <summary>
    /// Creating an organization is the only way anyone becomes its owner, so it is the only place
    /// the ownership grant can be recorded.
    /// </summary>
    [Fact]
    public async Task CreateOrganizationAsync_WithCreatorUserId_AuditsTheOwnerMark()
    {
        Guid creatorUserId = Guid.NewGuid();

        Guid result = await _sut.CreateOrganizationAsync("Test Org", creatorUserId: creatorUserId);

        await _messageBus.Received(1).PublishAsync(Arg.Is<MembershipTransitionedEvent>(e =>
            e.Transition == MembershipTransition.OwnerMarked &&
            e.OrganizationId == result &&
            e.TenantId == result &&
            e.UserId == creatorUserId &&
            e.ActorId == creatorUserId));
    }

    [Fact]
    public async Task CreateOrganizationAsync_WithoutCreatorUserId_DoesNotAddMemberAndAuditFieldsFallBackToEmpty()
    {
        Organization? capturedOrganization = null;
        _organizationRepository
            .When(r => r.Add(Arg.Any<Organization>()))
            .Do(call => capturedOrganization = call.Arg<Organization>());

        await _sut.CreateOrganizationAsync("Test Org");

        capturedOrganization.Should().NotBeNull();
        capturedOrganization!.CreatedBy.Should().Be(Guid.Empty);
        _dbContext.Memberships.IgnoreQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_WhenExists_ReturnsDto()
    {
        Guid orgId = Guid.NewGuid();
        Organization organization = Organization.Create(
            new TenantId(_tenantId), "Test Org", "test-org", Guid.NewGuid(), TimeProvider.System);

        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns(organization);

        OrganizationDto? result = await _sut.GetOrganizationByIdAsync(orgId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Org");
        result.MemberCount.Should().Be(0);
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_WhenNotFound_ReturnsNull()
    {
        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns((Organization?)null);

        OrganizationDto? result = await _sut.GetOrganizationByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrganizationsAsync_ReturnsMappedDtos()
    {
        List<Organization> organizations =
        [
            Organization.Create(new TenantId(_tenantId), "Org A", "org-a", Guid.NewGuid(), TimeProvider.System),
            Organization.Create(new TenantId(_tenantId), "Org B", "org-b", Guid.NewGuid(), TimeProvider.System)
        ];

        _organizationRepository.GetAllAsync(null, 0, 20, Arg.Any<CancellationToken>())
            .Returns(organizations);

        IReadOnlyList<OrganizationDto> result = await _sut.GetOrganizationsAsync();

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Org A");
        result[1].Name.Should().Be("Org B");
    }

    [Fact]
    public async Task AddMemberAsync_WhenOrgExists_AddsMemberAndPublishesEvent()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Organization organization = Organization.Create(
            new TenantId(_tenantId), "Test Org", "test-org", Guid.NewGuid(), TimeProvider.System);

        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns(organization);

        await _sut.AddMemberAsync(orgId, userId, "user", Guid.NewGuid());

        Membership? membership = await MembershipFor(userId, orgId);
        membership.Should().NotBeNull();
        membership!.IsActive.Should().BeTrue();
        membership.IsOwner.Should().BeFalse();
        membership.RoleIds.Should().BeEquivalentTo([RoleId("user")]);
        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationMemberAddedEvent>(e =>
            e.OrganizationId == orgId &&
            e.UserId == userId &&
            e.TenantId == orgId));
    }

    /// <summary>
    /// Bootstrap joins the organization the seed already created rather than minting a sibling,
    /// and what it needs there is exactly what creating one would have given: an Active owner
    /// membership carrying the admin role, audited as the owner's own act.
    /// </summary>
    [Fact]
    public async Task EnrollOwnerAsync_EnrollsAnActiveOwnerCarryingTheAdminRole()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Organization organization = Organization.Create(
            new TenantId(_tenantId), "Wallow", "wallow", Guid.Empty, TimeProvider.System);

        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns(organization);

        await _sut.EnrollOwnerAsync(orgId, userId);

        Membership? membership = await MembershipFor(userId, orgId);
        membership.Should().NotBeNull();
        membership!.IsActive.Should().BeTrue();
        membership.IsOwner.Should().BeTrue();
        membership.RoleIds.Should().BeEquivalentTo([RoleId("admin")]);
        membership.UpdatedBy.Should().Be(userId);
        await _messageBus.Received(1).PublishAsync(Arg.Is<MembershipTransitionedEvent>(e =>
            e.Transition == MembershipTransition.OwnerMarked &&
            e.OrganizationId == orgId &&
            e.UserId == userId &&
            e.ActorId == userId));
    }

    [Fact]
    public async Task EnrollOwnerAsync_WhenOrganizationDoesNotExist_Throws()
    {
        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns((Organization?)null);

        Func<Task> act = () => _sut.EnrollOwnerAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// The member does not add themselves - a reviewer or admin does. Stamping the membership with
    /// the subject hides who granted the access, which is the one thing the audit trail exists to
    /// answer.
    /// </summary>
    [Fact]
    public async Task AddMemberAsync_StampsTheMembershipWithTheActorNotTheSubject()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        Organization organization = Organization.Create(
            new TenantId(_tenantId), "Test Org", "test-org", Guid.NewGuid(), TimeProvider.System);

        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns(organization);

        await _sut.AddMemberAsync(orgId, userId, "user", actorId);

        Membership? membership = await MembershipFor(userId, orgId);
        membership.Should().NotBeNull();
        membership!.UpdatedBy.Should().Be(actorId);
    }

    [Fact]
    public async Task AddMemberAsync_WhenOrgNotFound_ThrowsInvalidOperationException()
    {
        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns((Organization?)null);

        Func<Task> act = () => _sut.AddMemberAsync(Guid.NewGuid(), Guid.NewGuid(), "user", Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Organization * not found");
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenOrgExists_RemovesMemberAndPublishesEvent()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Organization organization = Organization.Create(
            new TenantId(_tenantId), "Test Org", "test-org", Guid.NewGuid(), TimeProvider.System);

        _membershipRepository.Add(Membership.Enroll(
            userId, OrganizationId.Create(orgId), RoleId("user"), TimeProvider.System));
        await _membershipRepository.SaveChangesAsync();

        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns(organization);

        await _sut.RemoveMemberAsync(orgId, userId, Guid.NewGuid());

        (await MembershipFor(userId, orgId)).Should().BeNull();
        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationMemberRemovedEvent>(e =>
            e.OrganizationId == orgId &&
            e.UserId == userId &&
            e.TenantId == orgId));
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenOrgNotFound_ThrowsInvalidOperationException()
    {
        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns((Organization?)null);

        Func<Task> act = () => _sut.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Organization * not found");
    }

    [Fact]
    public async Task GetMembersAsync_WhenOrgNotFound_ReturnsEmpty()
    {
        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns((Organization?)null);

        IReadOnlyList<UserDto> result = await _sut.GetMembersAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMembersAsync_WhenNoMembers_ReturnsEmpty()
    {
        Organization organization = Organization.Create(
            new TenantId(_tenantId), "Test Org", "test-org", Guid.NewGuid(), TimeProvider.System);

        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns(organization);

        IReadOnlyList<UserDto> result = await _sut.GetMembersAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserOrganizationsAsync_ReturnsMappedDtos()
    {
        Guid userId = Guid.NewGuid();
        List<Organization> organizations =
        [
            Organization.Create(new TenantId(_tenantId), "Org A", "org-a", Guid.NewGuid(), TimeProvider.System)
        ];

        _organizationRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(organizations);

        IReadOnlyList<OrganizationDto> result = await _sut.GetUserOrganizationsAsync(userId);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Org A");
    }

    [Fact]
    public async Task GetMyOrganizationsAsync_NamesEachDoorAndTheStandingHeldBehindIt()
    {
        Guid userId = Guid.NewGuid();
        Organization owned = GivenOrganization("Org A", "org-a");
        Organization joined = GivenOrganization("Org B", "org-b");
        GivenMembership(userId, owned, isOwner: true);
        GivenMembership(userId, joined, isOwner: false);
        await _dbContext.SaveChangesAsync();
        _organizationRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns([owned, joined]);

        IReadOnlyList<MyOrganizationDto> result = await _sut.GetMyOrganizationsAsync(userId);

        result.Should().BeEquivalentTo(
        [
            new MyOrganizationDto(owned.Id.Value, "Org A", "org-a", true),
            new MyOrganizationDto(joined.Id.Value, "Org B", "org-b", false)
        ]);
    }

    /// <summary>
    /// Neither is a door: one has not been answered yet, and the other has closed. Both reach the
    /// mapping, because the organization read alone cannot tell them from a membership in good
    /// standing.
    /// </summary>
    [Fact]
    public async Task GetMyOrganizationsAsync_LeavesOutAPendingRequestAndAnArchivedOrganization()
    {
        Guid userId = Guid.NewGuid();
        Organization asked = GivenOrganization("Asked", "asked");
        Organization archived = GivenOrganization("Archived", "archived");
        _dbContext.Memberships.Add(Membership.RequestAccess(userId, asked.Id, TimeProvider.System));
        GivenMembership(userId, archived, isOwner: false);
        archived.Archive(Guid.NewGuid(), TimeProvider.System);
        await _dbContext.SaveChangesAsync();
        _organizationRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns([asked, archived]);

        IReadOnlyList<MyOrganizationDto> result = await _sut.GetMyOrganizationsAsync(userId);

        result.Should().BeEmpty();
    }

    private Organization GivenOrganization(string name, string slug)
    {
        Organization organization = Organization.Create(
            new TenantId(_tenantId), name, slug, Guid.NewGuid(), TimeProvider.System);
        _dbContext.Organizations.Add(organization);
        return organization;
    }

    private void GivenMembership(Guid userId, Organization organization, bool isOwner)
    {
        Guid roleId = _dbContext.Roles.IgnoreQueryFilters().Select(r => r.Id).First();
        Membership membership = Membership.Enroll(userId, organization.Id, roleId, TimeProvider.System);
        membership.MarkOwner(isOwner, userId, TimeProvider.System);
        _dbContext.Memberships.Add(membership);
    }
}
