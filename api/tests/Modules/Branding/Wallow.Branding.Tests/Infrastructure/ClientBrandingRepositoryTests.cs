using Microsoft.EntityFrameworkCore;
using Wallow.Branding.Domain.Entities;
using Wallow.Branding.Infrastructure.Persistence;
using Wallow.Branding.Infrastructure.Repositories;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Branding.Tests.Infrastructure;

public sealed class ClientBrandingRepositoryTests : IDisposable
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly BrandingDbContext _dbContext;
    private readonly ClientBrandingRepository _sut;

    public ClientBrandingRepositoryTests()
    {
        _dbContext = CreateDbContextForTenant(TenantId.New());
        _sut = new ClientBrandingRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    /// <summary>
    /// Opens a second context over the same in-memory store, acting as <paramref name="tenantId"/>.
    /// Pass <c>default</c> to model a request where no tenant resolved at all, which is what the
    /// anonymous branding endpoint does.
    /// </summary>
    private BrandingDbContext CreateDbContextForTenant(TenantId tenantId)
    {
        TenantContext tenantContext = new();
        if (tenantId != default)
        {
            tenantContext.SetTenant(tenantId);
        }

        TenantSaveChangesInterceptor tenantInterceptor = new(tenantContext);

        DbContextOptions<BrandingDbContext> options = new DbContextOptionsBuilder<BrandingDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .AddInterceptors(tenantInterceptor)
            .Options;

        BrandingDbContext context = new(options);
        context.SetTenant(tenantId);
        return context;
    }

    [Fact]
    public async Task GetByClientIdAsync_WhenExists_ReturnsBranding()
    {
        ClientBranding branding = ClientBranding.Create("client-1", "My App", "Tagline");
        _dbContext.ClientBrandings.Add(branding);
        await _dbContext.SaveChangesAsync();

        ClientBranding? result = await _sut.GetByClientIdAsync("client-1");

        result.Should().NotBeNull();
        result!.ClientId.Should().Be("client-1");
        result.DisplayName.Should().Be("My App");
    }

    [Fact]
    public async Task GetByClientIdAsync_WhenNotExists_ReturnsNull()
    {
        ClientBranding? result = await _sut.GetByClientIdAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task Add_PersistsBranding()
    {
        ClientBranding branding = ClientBranding.Create("client-1", "My App");

        _sut.Add(branding);
        await _sut.SaveChangesAsync();

        ClientBranding? found = await _sut.GetByClientIdAsync("client-1");
        found.Should().NotBeNull();
        found!.DisplayName.Should().Be("My App");
    }

    [Fact]
    public async Task Remove_DeletesBranding()
    {
        ClientBranding branding = ClientBranding.Create("client-1", "My App");
        _dbContext.ClientBrandings.Add(branding);
        await _dbContext.SaveChangesAsync();

        _sut.Remove(branding);
        await _sut.SaveChangesAsync();

        ClientBranding? found = await _sut.GetByClientIdAsync("client-1");
        found.Should().BeNull();
    }

    [Fact]
    public async Task GetByClientIdAsync_WithMultipleBrandings_ReturnsCorrectOne()
    {
        ClientBranding branding1 = ClientBranding.Create("client-1", "App One");
        ClientBranding branding2 = ClientBranding.Create("client-2", "App Two");
        _dbContext.ClientBrandings.Add(branding1);
        _dbContext.ClientBrandings.Add(branding2);
        await _dbContext.SaveChangesAsync();

        ClientBranding? result = await _sut.GetByClientIdAsync("client-2");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("App Two");
    }

    /// <summary>
    /// client_id is unique repo-wide, so branding does not partition by tenant and the lookup must
    /// find a row created by another organization. Writes are authorized on the OIDC application's
    /// creatorUserId, not on the ambient tenant, and a filtered miss here would send UpsertBranding
    /// down its insert branch and into that unique index.
    /// </summary>
    [Fact]
    public async Task GetByClientIdAsync_WhenBrandingBelongsToAnotherTenant_ReturnsBranding()
    {
        await using BrandingDbContext otherDbContext = CreateDbContextForTenant(TenantId.New());
        ClientBrandingRepository otherRepository = new(otherDbContext);

        otherRepository.Add(ClientBranding.Create("cross-tenant-client", "Cross Tenant"));
        await otherRepository.SaveChangesAsync();

        ClientBranding? result = await _sut.GetByClientIdAsync("cross-tenant-client");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Cross Tenant");
    }

    /// <summary>
    /// The public GET is AllowAnonymous, so no tenant resolves and the filter would compare
    /// tenant_id against default — matching nothing, then caching that null for five minutes.
    /// </summary>
    [Fact]
    public async Task GetByClientIdAsync_WhenNoTenantResolved_ReturnsBranding()
    {
        _dbContext.ClientBrandings.Add(ClientBranding.Create("client-1", "My App"));
        await _dbContext.SaveChangesAsync();

        await using BrandingDbContext anonymousDbContext = CreateDbContextForTenant(default);
        ClientBrandingRepository anonymousRepository = new(anonymousDbContext);

        ClientBranding? result = await anonymousRepository.GetByClientIdAsync("client-1");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("My App");
    }
}
