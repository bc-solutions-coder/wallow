using Microsoft.Extensions.DependencyInjection;
using Wallow.Tests.Common.Factories;

namespace Wallow.Tests.Common.Bases;

/// <summary>
/// Shared base for integration tests that use <see cref="WallowApiFactory"/>.
/// Handles HttpClient creation with auth, service scope lifecycle, and user context helpers.
/// Module-specific bases extend this and resolve their own services in <see cref="InitializeAsync"/>.
/// </summary>
[Trait("Category", "Integration")]
public abstract class WallowIntegrationTestBase : IAsyncLifetime
{
    protected WallowApiFactory Factory { get; }
    protected HttpClient Client { get; }
    private IServiceScope? _scope;
    protected IServiceProvider ScopedServices { get; private set; } = null!;

    protected WallowIntegrationTestBase(WallowApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        Client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
    }

    public virtual Task InitializeAsync()
    {
        _scope = Factory.Services.CreateScope();
        ScopedServices = _scope.ServiceProvider;
        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        _scope?.Dispose();
        return Task.CompletedTask;
    }

    protected void SetTestUser(string userId, string roles = "User")
    {
        // Convert non-GUID strings to deterministic GUIDs for consistent claim handling
        string userIdValue = Guid.TryParse(userId, out Guid parsed)
            ? parsed.ToString()
            : GenerateGuidFromString(userId).ToString();

        Client.DefaultRequestHeaders.Remove("X-Test-User-Id");
        Client.DefaultRequestHeaders.Remove("X-Test-Roles");
        Client.DefaultRequestHeaders.Remove("X-Test-Global-Admin");
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", userIdValue);
        Client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
    }

    /// <summary>
    /// Marks the current test principal as a global admin — the platform operator's own
    /// authority, carried as its own claim and never derived from organization roles.
    /// <see cref="SetTestUser"/> clears it, so becoming someone else drops the authority.
    /// </summary>
    protected void SetTestGlobalAdmin()
    {
        Client.DefaultRequestHeaders.Remove("X-Test-Global-Admin");
        Client.DefaultRequestHeaders.Add("X-Test-Global-Admin", "true");
    }

    protected void SetTestTenant(Guid tenantId)
    {
        Client.DefaultRequestHeaders.Remove("X-Test-Tenant-Id");
        Client.DefaultRequestHeaders.Add("X-Test-Tenant-Id", tenantId.ToString("D"));
    }

    /// <summary>
    /// Issues the test principal without any organization, as a first-party token without a hint
    /// is for a user with several (or no) memberships.
    /// </summary>
    protected void SetTestNoOrganization()
    {
        Client.DefaultRequestHeaders.Remove("X-Test-No-Organization");
        Client.DefaultRequestHeaders.Add("X-Test-No-Organization", "true");
    }

    private static Guid GenerateGuidFromString(string input)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash.AsSpan(0, 16));
    }
}
