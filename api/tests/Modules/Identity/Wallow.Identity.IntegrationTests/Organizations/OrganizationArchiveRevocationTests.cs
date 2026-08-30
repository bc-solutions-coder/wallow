using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Api.Services;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Identity.IntegrationTests.OrganizationClients;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Organizations;

/// <summary>
/// Archiving an organization takes back every credential that hangs off it: every bound client's
/// tokens and every member's tokens die, live realtime streams are hung up, and the authorize and
/// token endpoints refuse the organization's clients while it stays archived. Reactivating
/// restores every client the organization did not individually suspend — and only those.
/// </summary>
[Trait("Category", "Integration")]
public class OrganizationArchiveRevocationTests(WallowApiFactory factory) : OrganizationClientsTestBase(factory)
{
    private const string LoginScope = "openid offline_access";

    [Fact]
    public async Task Archive_RevokesBoundClientAndMemberTokens_AndReactivateRestoresAccess()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Archive Revocation Org");
        (Guid userId, string email) = await EnrollAndActAsync(orgId, "admin");
        (string clientId, string secret) = await RegisterApplicationAsync(orgId, "Archive App");
        TokenOutcome tokens = await SignInThroughAsync(email, clientId, secret);
        CancellationToken clientStream = OpenRealtimeStreamAsync(userId, orgId, clientId);
        CancellationToken memberStream = OpenRealtimeStreamAsync(userId, orgId, clientId: null);

        HttpResponseMessage archived = await Client.PostAsync($"/identity/organizations/{orgId}/archive", null);
        archived.StatusCode.Should().Be(HttpStatusCode.NoContent, await archived.Content.ReadAsStringAsync());

        await WaitForAsync(async () =>
            (await BearerCallAsync(tokens.AccessToken!)).StatusCode == HttpStatusCode.Unauthorized);
        (await BearerCallAsync(tokens.AccessToken!)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "an archived organization's member tokens are dead");

        TokenOutcome refresh = await Harness.RefreshAsync(clientId, secret, tokens.RefreshToken!);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized, refresh.Body);
        refresh.Error.Should().Be("invalid_client");

        AuthorizeOutcome refused = await Harness.AuthorizeAsync(clientId, LoginScope);
        refused.Error.Should().Be("organization_archived");
        refused.Location!.OriginalString.Should().NotStartWith(AuthorizationCodeFlowHarness.RedirectUri);
        refused.Location.OriginalString.Should().Contain("/error?");

        clientStream.IsCancellationRequested.Should().BeTrue("a bound client's realtime stream is hung up");
        memberStream.IsCancellationRequested.Should().BeTrue("a member's realtime stream is hung up");

        HttpResponseMessage reactivated = await Client.PostAsync($"/identity/organizations/{orgId}/reactivate", null);
        reactivated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Archive ended each member's standing the way a membership revocation does: the consent
        // authorization died with the tokens. The next sign-in grants consent afresh and gets
        // working tokens — the client itself is fully restored.
        AuthorizeOutcome again = await Harness.AuthorizeAsync(clientId, LoginScope);
        again.ConsentToken.Should().NotBeNull(again.Location?.ToString());
        AuthorizeOutcome granted = await Harness.ConsentAsync(again, grant: true);
        granted.Code.Should().NotBeNullOrEmpty(granted.Body);
        TokenOutcome fresh = await Harness.ExchangeCodeAsync(clientId, secret, granted.Code!, granted.CodeVerifier);
        fresh.StatusCode.Should().Be(HttpStatusCode.OK, fresh.Body);
    }

    [Fact]
    public async Task Reactivate_RestoresOnlyClientsTheOrganizationDidNotSuspendItself()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Archive Selective Restore Org");
        await ActAsEnrolledAsync(orgId, "admin");
        (string workingId, string workingSecret) = await RegisterServiceAccountAsync(orgId, "Working Worker");
        (string suspendedId, string suspendedSecret) = await RegisterServiceAccountAsync(orgId, "Suspended Worker");

        HttpResponseMessage suspend = await Client.PostAsync(
            $"/identity/organizations/{orgId}/clients/{suspendedId}/suspend", null);
        suspend.StatusCode.Should().Be(HttpStatusCode.OK, await suspend.Content.ReadAsStringAsync());
        (await ClientCredentialsAsync(workingId, workingSecret)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Client.PostAsync($"/identity/organizations/{orgId}/archive", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage refused = await ClientCredentialsAsync(workingId, workingSecret);
        refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an archived organization's service accounts get no tokens");
        (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString()
            .Should().Be("invalid_client");

        (await Client.PostAsync($"/identity/organizations/{orgId}/reactivate", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ClientCredentialsAsync(workingId, workingSecret)).StatusCode
            .Should().Be(HttpStatusCode.OK, "reactivation restores a client the organization never suspended");
        (await ClientCredentialsAsync(suspendedId, suspendedSecret)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "an individually suspended client stays suspended");
    }

    /// <summary>
    /// Registers a stream the way the SSE endpoint does, and hands back the token the endpoint
    /// would be waiting on. With a client id it is the stream a bound client opened; without one
    /// it is a member's own stream in the organization.
    /// </summary>
    private CancellationToken OpenRealtimeStreamAsync(Guid userId, Guid orgId, string? clientId)
    {
        SseConnectionManager connections = Factory.Services.GetRequiredService<SseConnectionManager>();
        string connectionId = $"archive-{Guid.NewGuid():N}";
        connections.AddConnection(connectionId, userId.ToString(), orgId, [], [], [], clientId);
        return connections.GetCancellationToken(connectionId);
    }
}
