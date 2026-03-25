using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Repositories;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

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

    #region ScimSyncLogRepository

    private static ScimSyncLog CreateScimSyncLog(Guid tenantId, string externalId, string? internalId, bool success, string? errorMessage = null)
    {
        return ScimSyncLog.Create(
            new TenantId(tenantId),
            ScimOperation.Create,
            ScimResourceType.User,
            externalId,
            internalId,
            success,
            TimeProvider.System,
            errorMessage);
    }

    [Fact]
    public async Task ScimSyncLogRepository_GetRecentAsync_ReturnsResults()
    {
        ScimSyncLog log1 = CreateScimSyncLog(_tenantId, "ext-1", "int-1", true);
        ScimSyncLog log2 = CreateScimSyncLog(_tenantId, "ext-2", "int-2", true);

        _dbContext.ScimSyncLogs.Add(log1);
        _dbContext.ScimSyncLogs.Add(log2);
        await _dbContext.SaveChangesAsync();

        ScimSyncLogRepository repo = new(_dbContext);
        IReadOnlyList<ScimSyncLog> result = await repo.GetRecentAsync(10);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScimSyncLogRepository_GetRecentAsync_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
        {
            ScimSyncLog log = CreateScimSyncLog(_tenantId, $"ext-{i}", $"int-{i}", true);
            _dbContext.ScimSyncLogs.Add(log);
        }
        await _dbContext.SaveChangesAsync();

        ScimSyncLogRepository repo = new(_dbContext);
        IReadOnlyList<ScimSyncLog> result = await repo.GetRecentAsync(3);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task ScimSyncLogRepository_Add_PersistsLog()
    {
        ScimSyncLog log = ScimSyncLog.Create(
            new TenantId(_tenantId),
            ScimOperation.Delete,
            ScimResourceType.Group,
            "ext-del",
            null,
            false,
            TimeProvider.System,
            errorMessage: "User not found");

        ScimSyncLogRepository repo = new(_dbContext);
        repo.Add(log);
        await repo.SaveChangesAsync();

        IReadOnlyList<ScimSyncLog> all = await repo.GetRecentAsync();
        all.Should().HaveCount(1);
        all[0].ExternalId.Should().Be("ext-del");
        all[0].Success.Should().BeFalse();
    }

    [Fact]
    public async Task ScimSyncLogRepository_GetRecentAsync_EmptyDb_ReturnsEmpty()
    {
        ScimSyncLogRepository repo = new(_dbContext);
        IReadOnlyList<ScimSyncLog> result = await repo.GetRecentAsync();

        result.Should().BeEmpty();
    }

    #endregion

    #region ScimConfigurationRepository

    [Fact]
    public async Task ScimConfigurationRepository_GetAsync_WhenNone_ReturnsNull()
    {
        ScimConfigurationRepository repo = new(_dbContext);
        ScimConfiguration? result = await repo.GetAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task ScimConfigurationRepository_Add_PersistsConfiguration()
    {
        (ScimConfiguration config, string _) = ScimConfiguration.Create(new TenantId(_tenantId), Guid.NewGuid(), TimeProvider.System);

        ScimConfigurationRepository repo = new(_dbContext);
        repo.Add(config);
        await repo.SaveChangesAsync();

        ScimConfiguration? found = await repo.GetAsync();
        found.Should().NotBeNull();
        found!.TenantId.Value.Should().Be(_tenantId);
    }

    #endregion

    #region MembershipRequestRepository

    private MembershipRequest CreateMembershipRequest(Guid? userId = null, string emailDomain = "example.com")
    {
        return MembershipRequest.Create(
            new TenantId(_tenantId),
            userId ?? Guid.NewGuid(),
            emailDomain,
            Guid.NewGuid(),
            TimeProvider.System);
    }

    [Fact]
    public async Task MembershipRequestRepository_GetByIdAsync_WhenExists_ReturnsRequest()
    {
        MembershipRequest request = CreateMembershipRequest();
        _dbContext.MembershipRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        MembershipRequestRepository repo = new(_dbContext);
        MembershipRequest? result = await repo.GetByIdAsync(request.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(request.Id);
    }

    [Fact]
    public async Task MembershipRequestRepository_GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        MembershipRequestRepository repo = new(_dbContext);
        MembershipRequest? result = await repo.GetByIdAsync(MembershipRequestId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task MembershipRequestRepository_GetByUserIdAsync_ReturnsMatchingRequests()
    {
        Guid userId = Guid.NewGuid();
        MembershipRequest request1 = CreateMembershipRequest(userId: userId);
        MembershipRequest request2 = CreateMembershipRequest(userId: userId);
        MembershipRequest request3 = CreateMembershipRequest(userId: Guid.NewGuid());
        _dbContext.MembershipRequests.AddRange(request1, request2, request3);
        await _dbContext.SaveChangesAsync();

        MembershipRequestRepository repo = new(_dbContext);
        List<MembershipRequest> result = await repo.GetByUserIdAsync(userId);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.UserId.Should().Be(userId));
    }

    [Fact]
    public async Task MembershipRequestRepository_GetByOrganizationIdAsync_ReturnsMatchingRequests()
    {
        OrganizationId orgId = OrganizationId.New();
        MembershipRequest request = CreateMembershipRequest();
        request.Approve(orgId, Guid.NewGuid(), TimeProvider.System);
        _dbContext.MembershipRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        MembershipRequestRepository repo = new(_dbContext);
        List<MembershipRequest> result = await repo.GetByOrganizationIdAsync(orgId);

        result.Should().HaveCount(1);
        result[0].ResolvedOrganizationId.Should().Be(orgId);
    }

    [Fact]
    public async Task MembershipRequestRepository_GetPendingAsync_ReturnsOnlyPending()
    {
        MembershipRequest pendingRequest = CreateMembershipRequest();
        MembershipRequest approvedRequest = CreateMembershipRequest();
        approvedRequest.Approve(OrganizationId.New(), Guid.NewGuid(), TimeProvider.System);
        _dbContext.MembershipRequests.AddRange(pendingRequest, approvedRequest);
        await _dbContext.SaveChangesAsync();

        MembershipRequestRepository repo = new(_dbContext);
        List<MembershipRequest> result = await repo.GetPendingAsync();

        result.Should().HaveCount(1);
        result[0].Status.Should().Be(MembershipRequestStatus.Pending);
    }

    [Fact]
    public async Task MembershipRequestRepository_GetPendingAsync_RespectsPagination()
    {
        for (int i = 0; i < 5; i++)
        {
            _dbContext.MembershipRequests.Add(CreateMembershipRequest());
        }
        await _dbContext.SaveChangesAsync();

        MembershipRequestRepository repo = new(_dbContext);
        List<MembershipRequest> result = await repo.GetPendingAsync(skip: 1, take: 2);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task MembershipRequestRepository_Add_PersistsRequest()
    {
        MembershipRequest request = CreateMembershipRequest(emailDomain: "test.org");

        MembershipRequestRepository repo = new(_dbContext);
        repo.Add(request);
        await repo.SaveChangesAsync();

        MembershipRequest? found = await repo.GetByIdAsync(request.Id);
        found.Should().NotBeNull();
        found!.EmailDomain.Should().Be("test.org");
    }

    [Fact]
    public async Task MembershipRequestRepository_GetByUserIdAsync_WhenNoMatches_ReturnsEmpty()
    {
        _dbContext.MembershipRequests.Add(CreateMembershipRequest());
        await _dbContext.SaveChangesAsync();

        MembershipRequestRepository repo = new(_dbContext);
        List<MembershipRequest> result = await repo.GetByUserIdAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MembershipRequestRepository_GetByOrganizationIdAsync_WhenNoMatches_ReturnsEmpty()
    {
        _dbContext.MembershipRequests.Add(CreateMembershipRequest());
        await _dbContext.SaveChangesAsync();

        MembershipRequestRepository repo = new(_dbContext);
        List<MembershipRequest> result = await repo.GetByOrganizationIdAsync(OrganizationId.New());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MembershipRequestRepository_GetPendingAsync_EmptyDb_ReturnsEmpty()
    {
        MembershipRequestRepository repo = new(_dbContext);
        List<MembershipRequest> result = await repo.GetPendingAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MembershipRequestRepository_GetPendingAsync_SkipBeyondCount_ReturnsEmpty()
    {
        _dbContext.MembershipRequests.Add(CreateMembershipRequest());
        await _dbContext.SaveChangesAsync();

        MembershipRequestRepository repo = new(_dbContext);
        List<MembershipRequest> result = await repo.GetPendingAsync(skip: 100, take: 20);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MembershipRequestRepository_SaveChangesAsync_PersistsModifications()
    {
        MembershipRequest request = CreateMembershipRequest();
        _dbContext.MembershipRequests.Add(request);
        await _dbContext.SaveChangesAsync();

        MembershipRequestRepository repo = new(_dbContext);
        MembershipRequest? tracked = await repo.GetByIdAsync(request.Id);
        tracked!.Approve(OrganizationId.New(), Guid.NewGuid(), TimeProvider.System);
        await repo.SaveChangesAsync();

        MembershipRequest? reloaded = await repo.GetByIdAsync(request.Id);
        reloaded!.Status.Should().Be(MembershipRequestStatus.Approved);
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
        org1.AddMember(userId, "admin", Guid.NewGuid(), TimeProvider.System);
        Organization org2 = CreateOrganization("Org B", "org-b");
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

        OrganizationRepository repo = new(_dbContext);
        repo.Add(org);
        await repo.SaveChangesAsync();

        Organization? found = await repo.GetByIdAsync(org.Id);
        found.Should().NotBeNull();
        found!.Slug.Should().Be("new-org");
    }

    #endregion

    #region OrganizationDomainRepository

    private OrganizationDomain CreateOrganizationDomain(
        OrganizationId? orgId = null,
        string domain = "example.com")
    {
        return OrganizationDomain.Create(
            new TenantId(_tenantId),
            orgId ?? OrganizationId.New(),
            domain,
            "verification-token-123",
            Guid.NewGuid(),
            TimeProvider.System);
    }

    [Fact]
    public async Task OrganizationDomainRepository_GetByIdAsync_WhenExists_ReturnsDomain()
    {
        OrganizationDomain orgDomain = CreateOrganizationDomain();
        _dbContext.OrganizationDomains.Add(orgDomain);
        await _dbContext.SaveChangesAsync();

        OrganizationDomainRepository repo = new(_dbContext);
        OrganizationDomain? result = await repo.GetByIdAsync(orgDomain.Id);

        result.Should().NotBeNull();
        result!.Domain.Should().Be("example.com");
    }

    [Fact]
    public async Task OrganizationDomainRepository_GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        OrganizationDomainRepository repo = new(_dbContext);
        OrganizationDomain? result = await repo.GetByIdAsync(OrganizationDomainId.New());

        result.Should().BeNull();
    }

    [Fact]
    public async Task OrganizationDomainRepository_GetByDomainAsync_WhenExists_ReturnsDomain()
    {
        OrganizationDomain orgDomain = CreateOrganizationDomain(domain: "test.org");
        _dbContext.OrganizationDomains.Add(orgDomain);
        await _dbContext.SaveChangesAsync();

        OrganizationDomainRepository repo = new(_dbContext);
        OrganizationDomain? result = await repo.GetByDomainAsync("TEST.ORG");

        result.Should().NotBeNull();
        result!.Domain.Should().Be("test.org");
    }

    [Fact]
    public async Task OrganizationDomainRepository_GetByDomainAsync_WhenNotExists_ReturnsNull()
    {
        OrganizationDomainRepository repo = new(_dbContext);
        OrganizationDomain? result = await repo.GetByDomainAsync("nonexistent.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task OrganizationDomainRepository_GetByOrganizationIdAsync_ReturnsMatchingDomains()
    {
        OrganizationId orgId = OrganizationId.New();
        OrganizationDomain domain1 = CreateOrganizationDomain(orgId: orgId, domain: "a.com");
        OrganizationDomain domain2 = CreateOrganizationDomain(orgId: orgId, domain: "b.com");
        OrganizationDomain domain3 = CreateOrganizationDomain(domain: "c.com");
        _dbContext.OrganizationDomains.AddRange(domain1, domain2, domain3);
        await _dbContext.SaveChangesAsync();

        OrganizationDomainRepository repo = new(_dbContext);
        List<OrganizationDomain> result = await repo.GetByOrganizationIdAsync(orgId);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(d => d.OrganizationId.Should().Be(orgId));
    }

    [Fact]
    public async Task OrganizationDomainRepository_Add_PersistsDomain()
    {
        OrganizationDomain orgDomain = CreateOrganizationDomain(domain: "new.io");

        OrganizationDomainRepository repo = new(_dbContext);
        repo.Add(orgDomain);
        await repo.SaveChangesAsync();

        OrganizationDomain? found = await repo.GetByIdAsync(orgDomain.Id);
        found.Should().NotBeNull();
        found!.Domain.Should().Be("new.io");
    }

    #endregion

    #region InitialAccessTokenRepository

    private static InitialAccessToken CreateInitialAccessToken(
        string hash = "abc123hash",
        string name = "Test Token",
        DateTimeOffset? expiresAt = null)
    {
        return InitialAccessToken.Create(hash, name, expiresAt);
    }

    [Fact]
    public async Task InitialAccessTokenRepository_AddAsync_PersistsToken()
    {
        InitialAccessToken token = CreateInitialAccessToken(hash: "hash-1", name: "Token 1");

        InitialAccessTokenRepository repo = new(_dbContext);
        await repo.AddAsync(token, CancellationToken.None);

        InitialAccessToken? found = await repo.GetByIdAsync(token.Id, CancellationToken.None);
        found.Should().NotBeNull();
        found!.DisplayName.Should().Be("Token 1");
    }

    [Fact]
    public async Task InitialAccessTokenRepository_GetByIdAsync_WhenNotExists_ReturnsNull()
    {
        InitialAccessTokenRepository repo = new(_dbContext);
        InitialAccessToken? result = await repo.GetByIdAsync(InitialAccessTokenId.New(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task InitialAccessTokenRepository_GetByHashAsync_WhenExists_ReturnsToken()
    {
        InitialAccessToken token = CreateInitialAccessToken(hash: "unique-hash");
        _dbContext.InitialAccessTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        InitialAccessTokenRepository repo = new(_dbContext);
        InitialAccessToken? result = await repo.GetByHashAsync("unique-hash", CancellationToken.None);

        result.Should().NotBeNull();
        result!.TokenHash.Should().Be("unique-hash");
    }

    [Fact]
    public async Task InitialAccessTokenRepository_GetByHashAsync_WhenNotExists_ReturnsNull()
    {
        InitialAccessTokenRepository repo = new(_dbContext);
        InitialAccessToken? result = await repo.GetByHashAsync("no-such-hash", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task InitialAccessTokenRepository_ListAsync_ReturnsToken()
    {
        _dbContext.InitialAccessTokens.Add(CreateInitialAccessToken(hash: "h1", name: "T1"));
        await _dbContext.SaveChangesAsync();

        InitialAccessTokenRepository repo = new(_dbContext);
        List<InitialAccessToken> result = await repo.ListAsync(CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].TokenHash.Should().Be("h1");
    }

    [Fact]
    public async Task InitialAccessTokenRepository_ListAsync_EmptyDb_ReturnsEmpty()
    {
        InitialAccessTokenRepository repo = new(_dbContext);
        List<InitialAccessToken> result = await repo.ListAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task InitialAccessTokenRepository_SaveChangesAsync_PersistsModifications()
    {
        InitialAccessToken token = CreateInitialAccessToken(hash: "save-hash", name: "Save Token");
        _dbContext.InitialAccessTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        InitialAccessTokenRepository repo = new(_dbContext);
        InitialAccessToken? tracked = await repo.GetByIdAsync(token.Id, CancellationToken.None);
        tracked!.Revoke();
        await repo.SaveChangesAsync(CancellationToken.None);

        InitialAccessToken? reloaded = await repo.GetByIdAsync(token.Id, CancellationToken.None);
        reloaded!.IsRevoked.Should().BeTrue();
    }

    #endregion
}
