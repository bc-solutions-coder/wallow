using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Identity.Infrastructure.MultiTenancy;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Where a request's tenant comes from once the auth cookie stops naming one. A machine caller
/// gets it from the application record, through the whole token endpoint; a cookie caller gets
/// none at all, because the person behind it belongs to many organizations. The second half is
/// only safe if a row cannot be written without a tenant, so that refusal is asserted too.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TenantResolutionSourceTests(WallowApiFactory factory)
    : IdentityIntegrationTestBase(factory)
{
    private const string ClientSecret = "tenant-resolution-secret";

    [Fact]
    public async Task ClientCredentialsToken_ForAServiceAccountBoundToAnOrganization_ResolvesOntoItsTenant()
    {
        Guid organizationId = Guid.NewGuid();
        string clientId = $"sa-tenant-resolution-{Guid.NewGuid():N}";
        await RegisterServiceAccountAsync(clientId, organizationId);

        string accessToken = await RequestClientCredentialsTokenAsync(clientId);

        AuthorizationCodeFlowHarness.ReadClaimValues(accessToken, "org_id")
            .Should().BeEquivalentTo([organizationId.ToString()]);

        TenantContext resolved = await ResolveAsync(
            AuthorizationCodeFlowHarness.ReadClaimValues(accessToken, "org_id")
                .Select(value => new Claim("org_id", value)));

        resolved.IsResolved.Should().BeTrue();
        resolved.TenantId.Value.Should().Be(organizationId);
    }

    [Fact]
    public async Task ClientCredentialsToken_ForAServiceAccountBoundToNoOrganization_ResolvesNoTenant()
    {
        string clientId = $"sa-tenant-resolution-{Guid.NewGuid():N}";
        await RegisterServiceAccountAsync(clientId, tenantId: null);

        string accessToken = await RequestClientCredentialsTokenAsync(clientId);

        AuthorizationCodeFlowHarness.ReadClaimValues(accessToken, "org_id").Should().BeEmpty();
    }

    [Fact]
    public async Task CookiePrincipal_CarryingNoOrgId_ResolvesNoTenant()
    {
        Guid userId = await AuthorizationCodeFlowHarness.CreateUserAsync(
            ScopedServices, $"cookie-tenant-{Guid.NewGuid():N}@wallow.dev", "Harness1234!");

        UserManager<WallowUser> users = ScopedServices.GetRequiredService<UserManager<WallowUser>>();
        WallowUser user = await users.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException(
            "The user the harness just created could not be read back.");

        IUserClaimsPrincipalFactory<WallowUser> claimsFactory =
            ScopedServices.GetRequiredService<IUserClaimsPrincipalFactory<WallowUser>>();
        ClaimsPrincipal principal = await claimsFactory.CreateAsync(user);

        TenantContext resolved = await ResolveAsync(principal.Claims);

        resolved.IsResolved.Should().BeFalse();
    }

    [Fact]
    public void OrganizationScopedRow_BuiltWithoutAResolvedTenant_IsRefused()
    {
        TenantContext unresolved = new();

        Action act = () => OrganizationSettings.Create(
            OrganizationId.New(),
            unresolved.TenantId,
            requireMfa: false,
            allowPasswordlessLogin: true,
            mfaGracePeriodDays: 7,
            Guid.NewGuid(),
            TimeProvider.System);

        act.Should().Throw<BusinessRuleException>().Which.Code.Should().Be("Shared.TenantRequired");
    }

    private static async Task<TenantContext> ResolveAsync(IEnumerable<Claim> claims)
    {
        DefaultHttpContext context = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")),
        };

        TenantContext tenantContext = new();
        TenantResolutionMiddleware middleware = new(
            _ => Task.CompletedTask, NullLogger<TenantResolutionMiddleware>.Instance);
        await middleware.InvokeAsync(context, tenantContext);

        return tenantContext;
    }

    private async Task RegisterServiceAccountAsync(string clientId, Guid? tenantId)
    {
        OpenIddictApplicationDescriptor descriptor = new()
        {
            ClientId = clientId,
            ClientSecret = ClientSecret,
            DisplayName = clientId,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
        };

        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "openid");

        if (tenantId is not null)
        {
            descriptor.SetTenantId(tenantId.Value.ToString());
        }

        IOpenIddictApplicationManager applications =
            ScopedServices.GetRequiredService<IOpenIddictApplicationManager>();
        await applications.CreateAsync(descriptor);
    }

    private async Task<string> RequestClientCredentialsTokenAsync(string clientId)
    {
        HttpClient tokenClient = Factory.CreateClient();
        tokenClient.DefaultRequestHeaders.Remove("Authorization");

        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = ClientSecret,
            ["scope"] = "openid",
        });

        HttpResponseMessage response = await tokenClient.PostAsync("/connect/token", content);
        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        TokenResponse? token = JsonSerializer.Deserialize<TokenResponse>(body);
        token.Should().NotBeNull();
        token.AccessToken.Should().NotBeNullOrWhiteSpace();

        return token.AccessToken;
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;
    }
}
