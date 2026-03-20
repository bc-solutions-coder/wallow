using Wallow.Identity.Domain.Entities;
using Wallow.Tests.Common.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace Wallow.Identity.IntegrationTests;

/// <summary>
/// Seeds test users via ASP.NET Core Identity and test OAuth2 clients via OpenIddict.
/// Used by integration tests that need realistic identity state without Keycloak.
/// </summary>
public sealed class IdentityFixture
{
    public const string TestUserEmail = "testuser@wallow.dev";
    public const string TestUserPassword = "Test1234!";
    public const string TestUserFirstName = "Test";
    public const string TestUserLastName = "User";

    public const string ServiceAccountClientId = "test-service-account";
    public const string ServiceAccountClientSecret = "test-service-secret";

    public const string ApiClientId = "wallow-api";
    public const string ApiClientSecret = "wallow-api-secret";

    public Guid TestUserId { get; private set; }

    public async Task SeedAsync(IServiceProvider services)
    {
        await SeedUsersAsync(services);
        await SeedClientsAsync(services);
    }

    private async Task SeedUsersAsync(IServiceProvider services)
    {
        UserManager<WallowUser> userManager = services.GetRequiredService<UserManager<WallowUser>>();

        WallowUser? existing = await userManager.FindByEmailAsync(TestUserEmail);
        if (existing is not null)
        {
            TestUserId = existing.Id;
            return;
        }

        WallowUser user = WallowUser.Create(
            TestConstants.TestTenantId,
            TestUserFirstName,
            TestUserLastName,
            TestUserEmail,
            TimeProvider.System);

        IdentityResult result = await userManager.CreateAsync(user, TestUserPassword);
        if (!result.Succeeded)
        {
            string errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed test user: {errors}");
        }

        TestUserId = user.Id;
    }

    private static async Task SeedClientsAsync(IServiceProvider services)
    {
        IOpenIddictApplicationManager appManager =
            services.GetRequiredService<IOpenIddictApplicationManager>();

        await EnsureClientAsync(appManager, ApiClientId, ApiClientSecret, "Wallow API");
        await EnsureClientAsync(appManager, ServiceAccountClientId, ServiceAccountClientSecret, "Test Service Account");
    }

    private static async Task EnsureClientAsync(
        IOpenIddictApplicationManager manager,
        string clientId,
        string clientSecret,
        string displayName)
    {
        object? existing = await manager.FindByClientIdAsync(clientId);
        if (existing is not null)
        {
            return;
        }

        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = displayName,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit
        };

        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "openid");

        await manager.CreateAsync(descriptor);
    }
}
