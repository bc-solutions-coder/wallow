using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Api.Services;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OrganizationClients;

/// <summary>
/// The lifecycle half of the org-scoped client surface: suspending a client ends every credential
/// it holds without touching its configuration or consents, reinstating brings it back exactly as
/// it was, and deleting removes it and everything that hung off it. Proven against the real
/// authorize and token endpoints, the real bearer validation, and the host's own realtime registry.
/// </summary>
[Trait("Category", "Integration")]
public class ClientLifecycleTests(WallowApiFactory factory) : OrganizationClientsTestBase(factory)
{
    private const string LoginScope = "openid offline_access";

    [Fact]
    public async Task Suspend_EndsEveryCredential_AndReinstate_RestoresLoginWithoutAskingConsentAgain()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Lifecycle Suspend Org");
        (Guid userId, string email) = await EnrollAndActAsync(orgId, "admin");
        (string clientId, string secret) = await RegisterApplicationAsync(orgId, "Suspend App");
        TokenOutcome tokens = await SignInThroughAsync(email, clientId, secret);
        CancellationToken stream = OpenRealtimeStreamAsync(userId, orgId, clientId);

        JsonElement suspended = await LifecycleAsync(orgId, clientId, "suspend");
        suspended.GetProperty("status").GetString().Should().Be("suspended");

        AuthorizeOutcome refused = await Harness.AuthorizeAsync(clientId, LoginScope);
        refused.Error.Should().Be("client_suspended");
        refused.Location!.OriginalString.Should().NotStartWith(AuthorizationCodeFlowHarness.RedirectUri);
        refused.Location.OriginalString.Should().Contain("/error?");

        TokenOutcome refresh = await Harness.RefreshAsync(clientId, secret, tokens.RefreshToken!);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized, refresh.Body);
        refresh.Error.Should().Be("invalid_client");

        (await BearerCallAsync(tokens.AccessToken!)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        stream.IsCancellationRequested.Should().BeTrue("a suspended client's realtime stream is hung up");

        JsonElement reinstated = await LifecycleAsync(orgId, clientId, "reinstate");
        reinstated.GetProperty("status").GetString().Should().Be("active");
        reinstated.GetProperty("redirectUris").GetArrayLength().Should().Be(1, "configuration survives a suspension");

        // The consent granted before the suspension is still on file: the authorize request goes
        // straight to a code instead of back through the consent screen.
        AuthorizeOutcome again = await Harness.AuthorizeAsync(clientId, LoginScope);
        again.ConsentToken.Should().BeNull(again.Location?.ToString());
        again.Code.Should().NotBeNullOrEmpty(again.Body);
        TokenOutcome fresh = await Harness.ExchangeCodeAsync(clientId, secret, again.Code!, again.CodeVerifier);
        fresh.StatusCode.Should().Be(HttpStatusCode.OK, fresh.Body);

        AuthAuditEntry suspendRow = await AuditRowAsync("ClientSuspended", clientId);
        suspendRow.ActorId.Should().Be(userId);
        suspendRow.TenantId.Should().Be(orgId);
        (await AuditRowAsync("ClientReinstated", clientId)).ActorId.Should().Be(userId);
    }

    [Fact]
    public async Task Suspend_RefusesAServiceAccount_UntilItIsReinstated()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Lifecycle Service Account Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, string secret) = await RegisterServiceAccountAsync(orgId, "Suspend Worker");
        (await ClientCredentialsAsync(clientId, secret)).StatusCode.Should().Be(HttpStatusCode.OK);

        await LifecycleAsync(orgId, clientId, "suspend");

        HttpResponseMessage refused = await ClientCredentialsAsync(clientId, secret);
        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()
            .Should().Be("invalid_client");

        await LifecycleAsync(orgId, clientId, "reinstate");
        (await ClientCredentialsAsync(clientId, secret)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Suspend_AndReinstate_RefuseARepeat()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Lifecycle Repeat Org");
        await ActAsEnrolledAsync(orgId, "admin");
        (string clientId, _) = await RegisterServiceAccountAsync(orgId, "Repeat Worker");

        (await Client.PostAsync($"/identity/organizations/{orgId}/clients/{clientId}/reinstate", null))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "an active client cannot be reinstated");
        await LifecycleAsync(orgId, clientId, "suspend");
        (await Client.PostAsync($"/identity/organizations/{orgId}/clients/{clientId}/suspend", null))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "a suspended client cannot be suspended again");
    }

    [Fact]
    public async Task Delete_EndsEveryCredential_RemovesBranding_AndAFreshRegistrationStartsClean()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Lifecycle Delete Org");
        (Guid userId, string email) = await EnrollAndActAsync(orgId, "admin");
        (string clientId, string secret) = await RegisterApplicationAsync(orgId, "Delete App");
        TokenOutcome tokens = await SignInThroughAsync(email, clientId, secret);
        // Registration itself creates the branding row through the integration event; wait for it.
        await WaitForAsync(async () => await BrandingOfAsync(clientId) is not null);
        (await BrandingOfAsync(clientId)).Should().NotBeNull("registration creates the branding row");

        HttpResponseMessage deleted = await Client.DeleteAsync($"/identity/organizations/{orgId}/clients/{clientId}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        AuthorizeOutcome unknown = await Harness.AuthorizeAsync(clientId, LoginScope);
        unknown.Code.Should().BeNull();
        // OpenIddict refuses an unknown client_id on the spot: no redirect anywhere, and the
        // request-level error rather than a client-authentication one.
        unknown.Body.Should().Contain("error:invalid_request", "a deleted client is an unknown client");
        unknown.Body.Should().Contain("The specified 'client_id' is invalid.");
        (unknown.Location?.OriginalString ?? string.Empty).Should().NotStartWith(AuthorizationCodeFlowHarness.RedirectUri);
        (await BearerCallAsync(tokens.AccessToken!)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        TokenOutcome refresh = await Harness.RefreshAsync(clientId, secret, tokens.RefreshToken!);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized, refresh.Body);
        refresh.Error.Should().Be("invalid_client");
        (await Client.GetAsync($"/identity/organizations/{orgId}/clients/{clientId}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        AuthAuditEntry deleteRow = await AuditRowAsync("ClientDeleted", clientId);
        deleteRow.ActorId.Should().Be(userId);
        deleteRow.TenantId.Should().Be(orgId);
        await WaitForAsync(async () => await BrandingOfAsync(clientId) is null);
        (await BrandingOfAsync(clientId)).Should().BeNull("branding goes with the client");

        // The same name derives the same id, and the new client carries none of the old one's consents.
        (string reborn, string rebornSecret) = await RegisterApplicationAsync(orgId, "Delete App");
        reborn.Should().Be(clientId);
        AuthorizeOutcome consent = await Harness.AuthorizeAsync(clientId, LoginScope);
        consent.ConsentToken.Should().NotBeNull("a fresh client has no consent on file");
        AuthorizeOutcome granted = await Harness.ConsentAsync(consent, grant: true);
        (await Harness.ExchangeCodeAsync(clientId, rebornSecret, granted.Code!, granted.CodeVerifier)).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Lifecycle_AnswersNotFound_ForAnotherOrganizationsClient_AndForbidsAMember()
    {
        Guid ownerOrg = await OrganizationOwnedBySomeoneElseAsync("Lifecycle Owner Org");
        await ActAsEnrolledAsync(ownerOrg, "admin");
        (string clientId, _) = await RegisterServiceAccountAsync(ownerOrg, "Lifecycle Owned Worker");

        Guid otherOrg = await OrganizationOwnedBySomeoneElseAsync("Lifecycle Other Org");
        await ActAsEnrolledAsync(otherOrg, "admin");
        (await Client.PostAsync($"/identity/organizations/{otherOrg}/clients/{clientId}/suspend", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.DeleteAsync($"/identity/organizations/{otherOrg}/clients/{clientId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await ActAsEnrolledAsync(ownerOrg, "user");
        (await Client.PostAsync($"/identity/organizations/{ownerOrg}/clients/{clientId}/suspend", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.DeleteAsync($"/identity/organizations/{ownerOrg}/clients/{clientId}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<JsonElement> LifecycleAsync(Guid orgId, string clientId, string action)
    {
        HttpResponseMessage response = await Client.PostAsync(
            $"/identity/organizations/{orgId}/clients/{clientId}/{action}", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Registers a stream the way the SSE endpoint does for a token the client issued, and hands
    /// back the token the endpoint would be waiting on.
    /// </summary>
    private CancellationToken OpenRealtimeStreamAsync(Guid userId, Guid orgId, string clientId)
    {
        SseConnectionManager connections = Factory.Services.GetRequiredService<SseConnectionManager>();
        string connectionId = $"lifecycle-{Guid.NewGuid():N}";
        connections.AddConnection(connectionId, userId.ToString(), orgId, [], [], [], clientId);
        return connections.GetCancellationToken(connectionId);
    }

    private async Task<ClientBranding?> BrandingOfAsync(string clientId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IClientBrandingRepository>().GetByClientIdAsync(clientId);
    }
}
