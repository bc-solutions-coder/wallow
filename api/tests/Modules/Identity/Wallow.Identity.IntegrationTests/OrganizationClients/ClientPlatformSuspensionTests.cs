using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Api.Services;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OrganizationClients;

/// <summary>
/// A platform suspension is the operator's own axis on a client, separate from the organization's
/// suspend/reinstate: only a global admin places or lifts it, it carries a reason the organization
/// can read but not remove, and while it stands the client behaves exactly as a suspended client —
/// no authorize, no tokens, no live streams.
/// </summary>
[Trait("Category", "Integration")]
public class ClientPlatformSuspensionTests(WallowApiFactory factory) : OrganizationClientsTestBase(factory)
{
    private const string LoginScope = "openid offline_access";

    [Fact]
    public async Task PlatformSuspension_RefusesTheClientEverywhere_AndTheOrganizationCannotLiftIt()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Platform Suspend Org");
        (Guid userId, string email) = await EnrollAndActAsync(orgId, "admin");
        (string clientId, string secret) = await RegisterApplicationAsync(orgId, "Platform Suspend App");
        TokenOutcome tokens = await SignInThroughAsync(email, clientId, secret);
        CancellationToken stream = OpenRealtimeStreamAsync(userId, orgId, clientId);

        Guid operatorId = await ActAsGlobalAdminAsync();
        HttpResponseMessage placed = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients/{clientId}/platform-suspension",
            new { reason = "Terms of service violation" });
        placed.StatusCode.Should().Be(HttpStatusCode.OK, await placed.Content.ReadAsStringAsync());
        JsonElement suspended = await placed.Content.ReadFromJsonAsync<JsonElement>();
        suspended.GetProperty("platformSuspensionReason").GetString().Should().Be("Terms of service violation");
        suspended.GetProperty("status").GetString().Should().Be(
            "active", "the platform's axis is separate from the organization's own status");

        AuthorizeOutcome refused = await Harness.AuthorizeAsync(clientId, LoginScope);
        refused.Error.Should().Be("client_suspended_by_platform");
        refused.Location!.OriginalString.Should().Contain("/error?");
        (await BearerCallAsync(tokens.AccessToken!)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        TokenOutcome refresh = await Harness.RefreshAsync(clientId, secret, tokens.RefreshToken!);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized, refresh.Body);
        refresh.Error.Should().Be("invalid_client");
        stream.IsCancellationRequested.Should().BeTrue("a platform-suspended client's realtime stream is hung up");

        // The organization's admin sees the reason on the client, but none of its own controls
        // lift a platform suspension: its reinstate answers as "nothing to reinstate", and the
        // platform-suspension resource itself is not theirs to touch.
        await ActAsEnrolledAsync(orgId, "admin");
        JsonElement seen = await GetClientAsync(orgId, clientId);
        seen.GetProperty("platformSuspensionReason").GetString().Should().Be("Terms of service violation");
        seen.GetProperty("platformSuspendedAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        (await Client.PostAsync($"/identity/organizations/{orgId}/clients/{clientId}/reinstate", null))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Client.PostAsJsonAsync(
                $"/identity/organizations/{orgId}/clients/{clientId}/platform-suspension",
                new { reason = "not mine to place" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Client.DeleteAsync($"/identity/organizations/{orgId}/clients/{clientId}/platform-suspension"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        Guid liftingOperatorId = await ActAsGlobalAdminAsync();
        HttpResponseMessage lifted = await Client.DeleteAsync(
            $"/identity/organizations/{orgId}/clients/{clientId}/platform-suspension");
        lifted.StatusCode.Should().Be(HttpStatusCode.OK, await lifted.Content.ReadAsStringAsync());
        JsonElement reinstated = await lifted.Content.ReadFromJsonAsync<JsonElement>();
        reinstated.GetProperty("platformSuspensionReason").ValueKind.Should().Be(JsonValueKind.Null);

        AuthorizeOutcome again = await Harness.AuthorizeAsync(clientId, LoginScope);
        again.ConsentToken.Should().BeNull("the consent granted before the suspension is still on file");
        again.Code.Should().NotBeNullOrEmpty(again.Body);
        (await Harness.ExchangeCodeAsync(clientId, secret, again.Code!, again.CodeVerifier)).StatusCode
            .Should().Be(HttpStatusCode.OK);

        AuthAuditEntry placedRow = await AuditRowAsync("ClientSuspendedByPlatform", clientId);
        placedRow.ActorId.Should().Be(operatorId);
        placedRow.TenantId.Should().Be(orgId);
        placedRow.Reason.Should().Be("Terms of service violation");
        AuthAuditEntry liftedRow = await AuditRowAsync("ClientReinstatedByPlatform", clientId);
        liftedRow.ActorId.Should().Be(liftingOperatorId);
        liftedRow.Reason.Should().BeNull("lifting needs no justification on the record; the placement carries it");
    }

    [Fact]
    public async Task PlatformSuspension_RequiresAGlobalAdmin_AReason_AndRefusesARepeat()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Platform Suspend Rules Org");
        await ActAsEnrolledAsync(orgId, "admin");
        (string clientId, string secret) = await RegisterServiceAccountAsync(orgId, "Platform Suspend Worker");

        await ActAsGlobalAdminAsync();
        (await Client.DeleteAsync($"/identity/organizations/{orgId}/clients/{clientId}/platform-suspension"))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "there is no suspension to lift");
        (await Client.PostAsJsonAsync(
                $"/identity/organizations/{orgId}/clients/{clientId}/platform-suspension",
                new { reason = " " }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "a platform suspension carries its reason");

        (await Client.PostAsJsonAsync(
                $"/identity/organizations/{orgId}/clients/{clientId}/platform-suspension",
                new { reason = "Abuse report" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.PostAsJsonAsync(
                $"/identity/organizations/{orgId}/clients/{clientId}/platform-suspension",
                new { reason = "Abuse report again" }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "one suspension is already in force");

        HttpResponseMessage refusedToken = await ClientCredentialsAsync(clientId, secret);
        refusedToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await refusedToken.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()
            .Should().Be("invalid_client");
    }

    private CancellationToken OpenRealtimeStreamAsync(Guid userId, Guid orgId, string clientId)
    {
        SseConnectionManager connections = Factory.Services.GetRequiredService<SseConnectionManager>();
        string connectionId = $"platform-{Guid.NewGuid():N}";
        connections.AddConnection(connectionId, userId.ToString(), orgId, [], [], [], clientId);
        return connections.GetCancellationToken(connectionId);
    }
}
