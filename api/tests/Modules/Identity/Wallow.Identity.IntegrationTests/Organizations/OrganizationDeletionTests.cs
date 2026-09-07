using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Wallow.Api.Services;
using Wallow.ApiKeys.Application.Interfaces;
using Wallow.ApiKeys.Domain.Entities;
using Wallow.ApiKeys.Infrastructure.Services;
using Wallow.Branding.Application.Interfaces;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Identity.IntegrationTests.OrganizationClients;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Shared.Kernel.Identity;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Organizations;

/// <summary>
/// Checks organization deletion, token refusal, account survival, and asynchronous branding/key cleanup.
/// Also checks rollback after an injected revocation failure and global-admin deletion while suspended.
/// </summary>
[Trait("Category", "Integration")]
public class OrganizationDeletionTests(WallowApiFactory factory) : OrganizationClientsTestBase(factory)
{
    private const string LoginScope = "openid offline_access";

    [Fact]
    public async Task Delete_EndsEveryCredential_RemovesEveryRow_AndLeavesMembersTheirAccounts()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Deletion Cascade Org");
        (Guid adminId, string email) = await EnrollAndActAsync(orgId, "admin");
        (string clientId, string secret) = await RegisterApplicationAsync(orgId, "Cascade App");
        (string workerId, string workerSecret) = await RegisterServiceAccountAsync(orgId, "Cascade Worker");
        TokenOutcome tokens = await SignInThroughAsync(email, clientId, secret);
        await HarnessTokenShouldBeAliveAsync(tokens.AccessToken!, "the token is alive until the organization dies");
        await WaitForAsync(async () => await BrandingOfAsync(clientId) is not null);
        CancellationToken clientStream = OpenRealtimeStreamAsync(adminId, orgId, clientId);
        CancellationToken memberStream = OpenRealtimeStreamAsync(adminId, orgId, clientId: null);

        // A case-mismatched confirmation name must leave the organization addressable.
        (await DeleteOrganizationAsync(Client, orgId, "deletion cascade org"))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "the typed name must match exactly");
        (await Client.GetAsync($"/identity/organizations/{orgId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage deleted = await DeleteOrganizationAsync(Client, orgId, "Deletion Cascade Org");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());

        await HarnessTokenShouldDieAsync(
            tokens.AccessToken!, "a deleted organization's member tokens are dead");
        TokenOutcome refresh = await Harness.RefreshAsync(clientId, secret, tokens.RefreshToken!);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized, refresh.Body);
        refresh.Error.Should().Be("invalid_client");

        // Deleted application registrations must no longer authorize or issue service tokens.
        AuthorizeOutcome unknown = await Harness.AuthorizeAsync(clientId, LoginScope);
        unknown.Code.Should().BeNull();
        unknown.Body.Should().Contain("error:invalid_request", "a deleted client is an unknown client");
        (await ClientCredentialsAsync(workerId, workerSecret)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "the organization's service accounts died with it");

        clientStream.IsCancellationRequested.Should().BeTrue("a bound client's realtime stream is hung up");
        memberStream.IsCancellationRequested.Should().BeTrue("a member's realtime stream is hung up");

        await ActAsGlobalAdminAsync();
        (await Client.GetAsync($"/identity/organizations/{orgId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound, "the organization row itself is gone");

        // Wait for event-driven branding cleanup.
        await WaitForAsync(async () => await BrandingOfAsync(clientId) is null);
        (await BrandingOfAsync(clientId)).Should().BeNull("the tenant's client branding goes with it");

        AuthAuditEntry row = await OrganizationAuditRowAsync("OrganizationDeleted", orgId);
        row.ActorId.Should().Be(adminId, "the audit names the admin who typed the name");

        // The former member must retain password sign-in and lose the organization listing.
        await Harness.SignInAsync(email, MemberPassword);
        SetTestUser(adminId.ToString(), "user");
        HttpResponseMessage mine = await Client.GetAsync("/identity/me/organizations");
        mine.StatusCode.Should().Be(HttpStatusCode.OK);
        (await mine.Content.ReadAsStringAsync()).Should().NotContain(orgId.ToString());
    }

    [Fact]
    public async Task Delete_OfAPlatformSuspendedOrganization_IsTheOperatorsAlone()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Deletion While Frozen Org");
        (Guid adminId, _) = await EnrollAndActAsync(orgId, "admin");

        await ActAsGlobalAdminAsync();
        HttpResponseMessage placed = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/platform-suspension",
            new { reason = "Under investigation" });
        placed.StatusCode.Should().Be(HttpStatusCode.NoContent, await placed.Content.ReadAsStringAsync());

        // Platform suspension must block deletion by the organization administrator.
        SetTestUser(adminId.ToString(), "admin");
        SetTestTenant(orgId);
        (await DeleteOrganizationAsync(Client, orgId, "Deletion While Frozen Org"))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "the freeze is not the admin's to end");
        (await Client.GetAsync($"/identity/organizations/{orgId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        await ActAsGlobalAdminAsync();
        HttpResponseMessage deleted = await DeleteOrganizationAsync(Client, orgId, "Deletion While Frozen Org");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());
        (await Client.GetAsync($"/identity/organizations/{orgId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_WhenRevocationFailsMidway_LeavesTheOrganizationIntact()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Deletion Rollback Org");
        (Guid adminId, string email) = await EnrollAndActAsync(orgId, "admin");
        (string clientId, string secret) = await RegisterApplicationAsync(orgId, "Rollback App");
        TokenOutcome tokens = await SignInThroughAsync(email, clientId, secret);
        await HarnessTokenShouldBeAliveAsync(tokens.AccessToken!, "the token is alive before the cascade is attempted");

        // Inject a realtime-revocation failure to exercise transaction rollback.
        using WebApplicationFactory<Program> failing = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRealtimeAccessRevoker>();
                services.AddSingleton<IRealtimeAccessRevoker>(new ExplodingRealtimeRevoker());
            }));
        using HttpClient failingClient = ActingClient(failing, adminId, "admin", orgId);

        (await DeleteOrganizationAsync(failingClient, orgId, "Deletion Rollback Org"))
            .StatusCode.Should().Be(HttpStatusCode.InternalServerError, "the cascade died mid-transaction");

        // After rollback, the organization and issued tokens must remain usable.
        (await Client.GetAsync($"/identity/organizations/{orgId}")).StatusCode
            .Should().Be(HttpStatusCode.OK, "a failed cascade leaves the organization intact");
        await HarnessTokenShouldBeAliveAsync(tokens.AccessToken!, "the rollback undid the token revocations");
        TokenOutcome refresh = await Harness.RefreshAsync(clientId, secret, tokens.RefreshToken!);
        refresh.StatusCode.Should().Be(HttpStatusCode.OK, refresh.Body);
    }

    [Fact]
    public async Task Delete_WithApiKeysEnabled_RevokesTheTenantsKeysAndDropsTheirCache()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Deletion ApiKeys Org");
        (Guid adminId, _) = await EnrollAndActAsync(orgId, "admin");

        // Enable the ApiKeys module on a derived host to exercise its deletion-event handler.
        using WebApplicationFactory<Program> withApiKeys = Factory.WithWebHostBuilder(builder =>
            builder.UseSetting("FeatureManagement:Modules.ApiKeys", "true"));

        string hashedKey = $"itest-hash-{Guid.NewGuid():N}";
        string cachedKeyId;
        IRedisDatabase redis = withApiKeys.Services.GetRequiredService<IRedisDatabase>();
        using (IServiceScope scope = withApiKeys.Services.CreateScope())
        {
            IApiKeyRepository keys = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
            keys.UseTenant(orgId);
            ApiKey key = ApiKey.Create(
                TenantId.Create(orgId), adminId.ToString(), hashedKey, "Cascade Key",
                ["organizations.read"], expiresAt: null, adminId, TimeProvider.System);
            await keys.AddAsync(key, CancellationToken.None);
            // Use the domain ID for the ID cache and user-key index.
            cachedKeyId = key.Id.Value.ToString();
        }

        string cachedJson = $$"""{"KeyId":"{{cachedKeyId}}","UserId":"{{adminId}}"}""";
        await redis.StringSetAsync($"apikey:{hashedKey}", cachedJson, null, false, When.Always, CommandFlags.None);
        await redis.StringSetAsync($"apikey:id:{cachedKeyId}", cachedJson, null, false, When.Always, CommandFlags.None);
        await redis.SetAddAsync($"apikeys:user:{adminId}", cachedKeyId);

        using HttpClient actingClient = ActingClient(withApiKeys, adminId, "admin", orgId);
        HttpResponseMessage deleted = await DeleteOrganizationAsync(actingClient, orgId, "Deletion ApiKeys Org");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());

        // Wait for the event handler to revoke the persisted key.
        await WaitForAsync(async () => (await TenantKeysAsync(withApiKeys, orgId)).All(k => k.IsRevoked));
        (await TenantKeysAsync(withApiKeys, orgId)).Should()
            .NotBeEmpty("the revoked rows remain as the audit trail")
            .And.OnlyContain(k => k.IsRevoked, "every key in the tenant is revoked");

        (await redis.StringGetAsync($"apikey:{hashedKey}")).IsNullOrEmpty
            .Should().BeTrue("the validation-path cache entry is gone");
        (await redis.StringGetAsync($"apikey:id:{cachedKeyId}")).IsNullOrEmpty
            .Should().BeTrue("the id-lookup cache entry is gone");
        (await redis.SetLengthAsync($"apikeys:user:{adminId}")).Should().Be(0, "the owner's key set is emptied");
    }

    private static async Task<List<ApiKey>> TenantKeysAsync(WebApplicationFactory<Program> host, Guid orgId)
    {
        using IServiceScope scope = host.Services.CreateScope();
        IApiKeyRepository keys = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
        keys.UseTenant(orgId);
        return await keys.ListByTenantAsync(orgId, CancellationToken.None);
    }

    /// <summary>
    /// Sends a DELETE request with the confirmation name in its JSON body.
    /// </summary>
    private static async Task<HttpResponseMessage> DeleteOrganizationAsync(
        HttpClient client, Guid orgId, string confirmName)
    {
        using HttpRequestMessage delete = new(HttpMethod.Delete, $"/identity/organizations/{orgId}")
        {
            Content = JsonContent.Create(new { confirmName }),
        };
        return await client.SendAsync(delete);
    }

    /// <summary>
    /// Creates a client for the derived host with explicit synthetic identity headers.
    /// </summary>
    private static HttpClient ActingClient(
        WebApplicationFactory<Program> host, Guid userId, string roles, Guid tenantId)
    {
        HttpClient client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", userId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        client.DefaultRequestHeaders.Add("X-Test-Tenant-Id", tenantId.ToString("D"));
        return client;
    }

    /// <summary>
    /// Adds an SSE registry entry and returns the cancellation token used by deletion checks.
    /// </summary>
    private CancellationToken OpenRealtimeStreamAsync(Guid userId, Guid orgId, string? clientId)
    {
        SseConnectionManager connections = Factory.Services.GetRequiredService<SseConnectionManager>();
        string connectionId = $"deletion-{Guid.NewGuid():N}";
        connections.AddConnection(connectionId, userId.ToString(), orgId, [], [], [], clientId);
        return connections.GetCancellationToken(connectionId);
    }

    private async Task<object?> BrandingOfAsync(string clientId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IClientBrandingRepository>()
            .GetByClientIdAsync(clientId);
    }

    private sealed class ExplodingRealtimeRevoker : IRealtimeAccessRevoker
    {
        public Task RevokeAsync(string userId, Guid tenantId, CancellationToken ct = default) =>
            throw new InvalidOperationException("Realtime is down; the cascade must not commit.");

        public Task RevokeClientAsync(string clientId, CancellationToken ct = default) =>
            throw new InvalidOperationException("Realtime is down; the cascade must not commit.");
    }
}
