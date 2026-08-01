using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
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
    private readonly IMembershipAccessRevoker _accessRevoker;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly TenantContext _tenantContextInstance;

    public OrganizationServiceTests()
    {
        _tenantContextInstance = new TenantContext();
        _tenantContextInstance.SetTenant(new TenantId(_tenantId));

        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Identity.Tests");
        _dbContext = new IdentityDbContext(options, dataProtectionProvider);
        _dbContext.SetTenant(new TenantId(_tenantId));

        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _membershipRepository = new MembershipRepository(_dbContext);
        _messageBus = Substitute.For<IMessageBus>();
        _accessRevoker = Substitute.For<IMembershipAccessRevoker>();

        SeedRoleCatalog();

        _sut = new OrganizationService(
            _organizationRepository,
            _membershipRepository,
            _dbContext,
            _accessRevoker,
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

        await _sut.AddMemberAsync(orgId, userId, "user");

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

    [Fact]
    public async Task AddMemberAsync_WhenOrgNotFound_ThrowsInvalidOperationException()
    {
        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns((Organization?)null);

        Func<Task> act = () => _sut.AddMemberAsync(Guid.NewGuid(), Guid.NewGuid(), "user");

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

        await _sut.RemoveMemberAsync(orgId, userId);

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

        Func<Task> act = () => _sut.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid());

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
}
