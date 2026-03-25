using Microsoft.EntityFrameworkCore;
using Wallow.Branding.Domain.Entities;
using Wallow.Branding.Infrastructure.Persistence;
using Wallow.Branding.Infrastructure.Repositories;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Branding.Tests.Infrastructure;

public sealed class ClientBrandingRepositoryTests : IDisposable
{
    private readonly BrandingDbContext _dbContext;
    private readonly ClientBrandingRepository _sut;

    public ClientBrandingRepositoryTests()
    {
        TenantContext tenantContext = new();
        tenantContext.SetTenant(TenantId.New());

        TenantSaveChangesInterceptor tenantInterceptor = new(tenantContext);

        DbContextOptions<BrandingDbContext> options = new DbContextOptionsBuilder<BrandingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(tenantInterceptor)
            .Options;

        _dbContext = new BrandingDbContext(options);
        _dbContext.SetTenant(tenantContext.TenantId);
        _sut = new ClientBrandingRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
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
}
