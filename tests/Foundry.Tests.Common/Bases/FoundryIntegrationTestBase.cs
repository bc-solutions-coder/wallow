using Foundry.Tests.Common.Factories;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Tests.Common.Bases;

/// <summary>
/// Shared base for integration tests that use <see cref="FoundryApiFactory"/>.
/// Handles HttpClient creation with auth, service scope lifecycle, and user context helpers.
/// Module-specific bases extend this and resolve their own services in <see cref="InitializeAsync"/>.
/// </summary>
[Trait("Category", "Integration")]
public abstract class FoundryIntegrationTestBase : IAsyncLifetime
{
    protected FoundryApiFactory Factory { get; }
    protected HttpClient Client { get; }
    private IServiceScope? _scope;
    protected IServiceProvider ScopedServices { get; private set; } = null!;

    protected FoundryIntegrationTestBase(FoundryApiFactory factory)
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
        Client.DefaultRequestHeaders.Add("X-Test-User-Id", userIdValue);
        Client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
    }

    private static Guid GenerateGuidFromString(string input)
    {
        byte[] hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash.AsSpan(0, 16));
    }
}
