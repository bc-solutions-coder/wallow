using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Tests.Infrastructure;

public sealed class RepositoryTests : IDisposable
{
    private readonly IdentityDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Guid _tenantId = Guid.NewGuid();

    public RepositoryTests()
    {
        _tenantContext = new TenantContext();
        _tenantContext.SetTenant(new TenantId(_tenantId));

        DbContextOptions<IdentityDbContext> options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        IDataProtectionProvider dataProtectionProvider = DataProtectionProvider.Create("Wallow.Identity.Tests");
        _dbContext = new IdentityDbContext(options, dataProtectionProvider);
        _dbContext.SetTenant(new TenantId(_tenantId));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    #region ApiScopeRepository

    [Fact]
    public async Task ApiScopeRepository_GetAllAsync_ReturnsAllScopes()
    {
        ApiScope scope1 = ApiScope.Create("invoices.read", "Read Invoices", "Billing");
        ApiScope scope2 = ApiScope.Create("users.read", "Read Users", "Identity");
        _dbContext.ApiScopes.Add(scope1);
        _dbContext.ApiScopes.Add(scope2);
        await _dbContext.SaveChangesAsync();

        ApiScopeRepository repo = new(_dbContext);
        IReadOnlyList<ApiScope> result = await repo.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ApiScopeRepository_GetAllAsync_WithCategory_FiltersCorrectly()
    {
        ApiScope scope1 = ApiScope.Create("invoices.read", "Read Invoices", "Billing");
        ApiScope scope2 = ApiScope.Create("users.read", "Read Users", "Identity");
        _dbContext.ApiScopes.Add(scope1);
        _dbContext.ApiScopes.Add(scope2);
        await _dbContext.SaveChangesAsync();

        ApiScopeRepository repo = new(_dbContext);
        IReadOnlyList<ApiScope> result = await repo.GetAllAsync(category: "Billing");

        result.Should().HaveCount(1);
        result[0].Code.Should().Be("invoices.read");
    }

    [Fact]
    public async Task ApiScopeRepository_GetByCodesAsync_ReturnMatchingScopes()
    {
        ApiScope scope1 = ApiScope.Create("invoices.read", "Read Invoices", "Billing");
        ApiScope scope2 = ApiScope.Create("users.read", "Read Users", "Identity");
        ApiScope scope3 = ApiScope.Create("payments.write", "Write Payments", "Billing");
        _dbContext.ApiScopes.Add(scope1);
        _dbContext.ApiScopes.Add(scope2);
        _dbContext.ApiScopes.Add(scope3);
        await _dbContext.SaveChangesAsync();

        ApiScopeRepository repo = new(_dbContext);
        IReadOnlyList<ApiScope> result = await repo.GetByCodesAsync(["invoices.read", "payments.write"]);

        result.Should().HaveCount(2);
        result.Select(s => s.Code).Should().Contain("invoices.read");
        result.Select(s => s.Code).Should().Contain("payments.write");
    }

    [Fact]
    public async Task ApiScopeRepository_Add_PersistsScope()
    {
        ApiScope scope = ApiScope.Create("webhooks.manage", "Manage Webhooks", "Platform");

        ApiScopeRepository repo = new(_dbContext);
        repo.Add(scope);
        await repo.SaveChangesAsync();

        IReadOnlyList<ApiScope> all = await repo.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Code.Should().Be("webhooks.manage");
    }

    [Fact]
    public async Task ApiScopeRepository_GetAllAsync_EmptyDb_ReturnsEmpty()
    {
        ApiScopeRepository repo = new(_dbContext);
        IReadOnlyList<ApiScope> result = await repo.GetAllAsync();

        result.Should().BeEmpty();
    }

    #endregion

    #region InvitationRepository

    private Invitation CreateInvitation(string email = "user@example.com")
    {
        return Invitation.Create(
            new TenantId(_tenantId),
            email,
            DateTimeOffset.UtcNow.AddDays(7),
            Guid.NewGuid(),
            TimeProvider.System);
    }

    [Fact]
    public async Task InvitationRepository_GetByIdAsync_WhenExists_ReturnsInvitation()
    {
        Invitation invitation = CreateInvitation();
        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        InvitationRepository repo = new(_dbContext);
        Invitation? result = await repo.GetByIdAsync(invitation.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(invitation.Id);
    }

    [Fact]
    public async Task InvitationRepository_GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        InvitationRepository repo = new(_dbContext);
        Invitation? result = await repo.GetByIdAsync(InvitationId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task InvitationRepository_GetByTokenAsync_WhenExists_ReturnsInvitation()
    {
        Invitation invitation = CreateInvitation();
        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        InvitationRepository repo = new(_dbContext);
        Invitation? result = await repo.GetByTokenAsync(invitation.Token);

        result.Should().NotBeNull();
        result!.Token.Should().Be(invitation.Token);
    }

    [Fact]
    public async Task InvitationRepository_GetByTokenAsync_WhenNotExists_ReturnsNull()
    {
        InvitationRepository repo = new(_dbContext);
        Invitation? result = await repo.GetByTokenAsync("nonexistent-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task InvitationRepository_GetPagedByTenantAsync_ReturnsPaged()
    {
        for (int i = 0; i < 5; i++)
        {
            _dbContext.Invitations.Add(CreateInvitation($"user{i}@example.com"));
        }
        await _dbContext.SaveChangesAsync();

        InvitationRepository repo = new(_dbContext);
        List<Invitation> result = await repo.GetPagedByTenantAsync(_tenantId, skip: 1, take: 3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task InvitationRepository_Add_PersistsInvitation()
    {
        Invitation invitation = CreateInvitation("new@example.com");

        InvitationRepository repo = new(_dbContext);
        repo.Add(invitation);
        await repo.SaveChangesAsync();

        Invitation? found = await repo.GetByIdAsync(invitation.Id);
        found.Should().NotBeNull();
        found!.Email.Should().Be("new@example.com");
    }

    [Fact]
    public async Task InvitationRepository_Delete_RemovesInvitation()
    {
        Invitation invitation = CreateInvitation();
        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        InvitationRepository repo = new(_dbContext);
        Invitation? toDelete = await repo.GetByIdAsync(invitation.Id);
        repo.Delete(toDelete!);
        await repo.SaveChangesAsync();

        Invitation? found = await repo.GetByIdAsync(invitation.Id);
        found.Should().BeNull();
    }

    [Fact]
    public async Task InvitationRepository_GetPagedByTenantAsync_EmptyDb_ReturnsEmpty()
    {
        InvitationRepository repo = new(_dbContext);
        List<Invitation> result = await repo.GetPagedByTenantAsync(_tenantId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task InvitationRepository_GetPagedByTenantAsync_SkipBeyondCount_ReturnsEmpty()
    {
        _dbContext.Invitations.Add(CreateInvitation());
        await _dbContext.SaveChangesAsync();

        InvitationRepository repo = new(_dbContext);
        List<Invitation> result = await repo.GetPagedByTenantAsync(_tenantId, skip: 100, take: 20);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task InvitationRepository_GetPagedByTenantAsync_DefaultPagination_ReturnsTwentyOrLess()
    {
        for (int i = 0; i < 25; i++)
        {
            _dbContext.Invitations.Add(CreateInvitation($"user{i}@example.com"));
        }
        await _dbContext.SaveChangesAsync();

        InvitationRepository repo = new(_dbContext);
        List<Invitation> result = await repo.GetPagedByTenantAsync(_tenantId);

        result.Should().HaveCount(20);
    }

    [Fact]
    public async Task InvitationRepository_SaveChangesAsync_PersistsModifications()
    {
        Invitation invitation = CreateInvitation();
        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync();

        InvitationRepository repo = new(_dbContext);
        Invitation? tracked = await repo.GetByIdAsync(invitation.Id);
        tracked!.Accept(Guid.NewGuid(), TimeProvider.System);
        await repo.SaveChangesAsync();

        Invitation? reloaded = await repo.GetByIdAsync(invitation.Id);
        reloaded!.Status.Should().Be(InvitationStatus.Accepted);
    }

    #endregion

    #region OrganizationRepository

    private Organization CreateOrganization(string name = "Test Org", string slug = "test-org")
    {
        return Organization.Create(
            new TenantId(_tenantId),
            name,
            slug,
            Guid.NewGuid(),
            TimeProvider.System);
    }

    [Fact]
    public async Task OrganizationRepository_GetByIdAsync_WhenExists_ReturnsOrganization()
    {
        Organization org = CreateOrganization();
        // org IS the tenant: Organization.Create mints TenantId == org.Id, so the
        // ambient tenant must be org.Id for the query filter to match this row.
        _dbContext.SetTenant(new TenantId(org.Id.Value));
        _dbContext.Organizations.Add(org);
        await _dbContext.SaveChangesAsync();

        OrganizationRepository repo = new(_dbContext);
        Organization? result = await repo.GetByIdAsync(org.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Org");
    }

    [Fact]
    public async Task OrganizationRepository_GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        OrganizationRepository repo = new(_dbContext);
        Organization? result = await repo.GetByIdAsync(OrganizationId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task OrganizationRepository_GetAllAsync_ReturnsAll()
    {
        _dbContext.Organizations.Add(CreateOrganization("Alpha", "alpha"));
        _dbContext.Organizations.Add(CreateOrganization("Beta", "beta"));
        await _dbContext.SaveChangesAsync();

        OrganizationRepository repo = new(_dbContext);
        List<Organization> result = await repo.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task OrganizationRepository_GetAllAsync_WithSearch_FiltersCorrectly()
    {
        _dbContext.Organizations.Add(CreateOrganization("Alpha Corp", "alpha-corp"));
        _dbContext.Organizations.Add(CreateOrganization("Beta Inc", "beta-inc"));
        await _dbContext.SaveChangesAsync();

        OrganizationRepository repo = new(_dbContext);
        List<Organization> result = await repo.GetAllAsync(search: "Alpha");

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Alpha Corp");
    }

    [Fact]
    public async Task OrganizationRepository_GetAllAsync_RespectsPagination()
    {
        for (int i = 0; i < 5; i++)
        {
            _dbContext.Organizations.Add(CreateOrganization($"Org {i}", $"org-{i}"));
        }
        await _dbContext.SaveChangesAsync();

        OrganizationRepository repo = new(_dbContext);
        List<Organization> result = await repo.GetAllAsync(skip: 2, take: 2);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task OrganizationRepository_GetByUserIdAsync_ReturnsOrgsForUser()
    {
        Guid userId = Guid.NewGuid();
        Organization org1 = CreateOrganization("Org A", "org-a");
        org1.AddMember(userId, OrgMemberRole.Admin, Guid.NewGuid(), TimeProvider.System);
        Organization org2 = CreateOrganization("Org B", "org-b");
        // org IS the tenant: set the ambient tenant to the org whose membership we query.
        _dbContext.SetTenant(new TenantId(org1.Id.Value));
        _dbContext.Organizations.AddRange(org1, org2);
        await _dbContext.SaveChangesAsync();

        OrganizationRepository repo = new(_dbContext);
        List<Organization> result = await repo.GetByUserIdAsync(userId);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Org A");
    }

    [Fact]
    public async Task OrganizationRepository_Add_PersistsOrganization()
    {
        Organization org = CreateOrganization("New Org", "new-org");
        // org IS the tenant: align the ambient tenant to org.Id so the persisted row is visible.
        _dbContext.SetTenant(new TenantId(org.Id.Value));

        OrganizationRepository repo = new(_dbContext);
        repo.Add(org);
        await repo.SaveChangesAsync();

        Organization? found = await repo.GetByIdAsync(org.Id);
        found.Should().NotBeNull();
        found!.Slug.Should().Be("new-org");
    }

    #endregion
}
