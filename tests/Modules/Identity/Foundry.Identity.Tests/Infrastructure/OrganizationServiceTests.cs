using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Identity;
using Foundry.Identity.Infrastructure.Persistence;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Contracts.Identity.Events;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wolverine;

namespace Foundry.Identity.Tests.Infrastructure;

public sealed class OrganizationServiceTests : IDisposable
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IdentityDbContext _dbContext;
    private readonly IMessageBus _messageBus;
    private readonly ITenantContext _tenantContext;
    private readonly OrganizationService _sut;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly TenantContext _tenantContextInstance;

    public OrganizationServiceTests()
    {
        _tenantContextInstance = new TenantContext();
        _tenantContextInstance.SetTenant(new TenantId(_tenantId));
        _tenantContext = _tenantContextInstance;

        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Foundry.Identity.Tests");
        _dbContext = new IdentityDbContext(options, _tenantContext, dataProtectionProvider);

        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _messageBus = Substitute.For<IMessageBus>();

        _sut = new OrganizationService(
            _organizationRepository,
            _dbContext,
            _messageBus,
            _tenantContext,
            TimeProvider.System,
            NullLogger<OrganizationService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

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
            e.TenantId == _tenantId));
    }

    [Fact]
    public async Task CreateOrganizationAsync_WithNullCreatorEmail_UsesEmptyString()
    {
        Guid result = await _sut.CreateOrganizationAsync("Test Org");

        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationCreatedEvent>(e =>
            e.CreatorEmail == string.Empty));
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_WhenExists_ReturnsDto()
    {
        Guid orgId = Guid.NewGuid();
        OrganizationId id = OrganizationId.Create(orgId);
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

        await _sut.AddMemberAsync(orgId, userId);

        organization.Members.Should().HaveCount(1);
        organization.Members[0].UserId.Should().Be(userId);
        await _organizationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationMemberAddedEvent>(e =>
            e.OrganizationId == orgId &&
            e.UserId == userId &&
            e.TenantId == _tenantId));
    }

    [Fact]
    public async Task AddMemberAsync_WhenOrgNotFound_ThrowsInvalidOperationException()
    {
        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns((Organization?)null);

        Func<Task> act = () => _sut.AddMemberAsync(Guid.NewGuid(), Guid.NewGuid());

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
        organization.AddMember(userId, "member", Guid.NewGuid(), TimeProvider.System);

        _organizationRepository.GetByIdAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
            .Returns(organization);

        await _sut.RemoveMemberAsync(orgId, userId);

        organization.Members.Should().BeEmpty();
        await _organizationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _messageBus.Received(1).PublishAsync(Arg.Is<OrganizationMemberRemovedEvent>(e =>
            e.OrganizationId == orgId &&
            e.UserId == userId &&
            e.TenantId == _tenantId));
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
