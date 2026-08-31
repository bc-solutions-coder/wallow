using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Api.Services;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Identity.IntegrationTests.OrganizationClients;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.Organizations;

/// <summary>
/// A platform suspension of an organization is the operator's freeze: every bound client's and
/// every member's tokens die the moment it lands, the organization's endpoints refuse every
/// change while it stands, and its admins read the reason but cannot lift it. Deletion under
/// the freeze is the operator's alone — <see cref="OrganizationDeletionTests"/> covers it.
/// </summary>
[Trait("Category", "Integration")]
public class OrganizationPlatformSuspensionTests(WallowApiFactory factory) : OrganizationClientsTestBase(factory)
{
    private const string LoginScope = "openid offline_access";

    [Fact]
    public async Task PlatformSuspension_KillsEveryTokenAndFreezesTheOrganization_UntilLifted()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Org Platform Suspend Org");
        (Guid userId, string email) = await EnrollAndActAsync(orgId, "admin");
        (string clientId, string secret) = await RegisterApplicationAsync(orgId, "Org Suspend App");
        (string workerId, string workerSecret) = await RegisterServiceAccountAsync(orgId, "Org Suspend Worker");
        TokenOutcome tokens = await SignInThroughAsync(email, clientId, secret);
        await HarnessTokenShouldBeAliveAsync(
            tokens.AccessToken!, "the token is alive until the platform suspends the organization");
        CancellationToken memberStream = OpenRealtimeStreamAsync(userId, orgId);

        Guid operatorId = await ActAsGlobalAdminAsync();
        HttpResponseMessage placed = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/platform-suspension",
            new { reason = "Fraud investigation" });
        placed.StatusCode.Should().Be(HttpStatusCode.NoContent, await placed.Content.ReadAsStringAsync());

        await HarnessTokenShouldDieAsync(
            tokens.AccessToken!, "a platform-suspended organization's member tokens are dead");
        TokenOutcome refresh = await Harness.RefreshAsync(clientId, secret, tokens.RefreshToken!);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized, refresh.Body);
        refresh.Error.Should().Be("invalid_client");
        (await ClientCredentialsAsync(workerId, workerSecret)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "a bound service account gets no tokens either");

        AuthorizeOutcome refused = await Harness.AuthorizeAsync(clientId, LoginScope);
        refused.Error.Should().Be("organization_suspended_by_platform");
        memberStream.IsCancellationRequested.Should().BeTrue("a member's realtime stream is hung up");

        // The organization's own admin reads the operator's reason on the organization, but every
        // change is refused while the freeze stands — the suspension resource included, so the org
        // surface cannot lift it.
        await ActAsEnrolledAsync(orgId, "admin");
        HttpResponseMessage seen = await Client.GetAsync($"/identity/organizations/{orgId}");
        seen.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement org = await seen.Content.ReadFromJsonAsync<JsonElement>();
        org.GetProperty("platformSuspensionReason").GetString().Should().Be("Fraud investigation");
        (await Client.PostAsync($"/identity/organizations/{orgId}/archive", null))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Client.DeleteAsync($"/identity/organizations/{orgId}/platform-suspension"))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "the freeze answers before the forbidden lift");

        Guid liftingOperatorId = await ActAsGlobalAdminAsync();
        (await Client.DeleteAsync($"/identity/organizations/{orgId}/platform-suspension"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ClientCredentialsAsync(workerId, workerSecret)).StatusCode
            .Should().Be(HttpStatusCode.OK, "the lift restores a client the organization never suspended itself");

        AuthAuditEntry placedRow = await OrganizationAuditRowAsync("OrganizationSuspendedByPlatform", orgId);
        placedRow.ActorId.Should().Be(operatorId);
        placedRow.Reason.Should().Be("Fraud investigation");
        AuthAuditEntry liftedRow = await OrganizationAuditRowAsync("OrganizationReinstatedByPlatform", orgId);
        liftedRow.ActorId.Should().Be(liftingOperatorId);
        liftedRow.Reason.Should().BeNull("lifting needs no justification on the record; the placement carries it");
    }

    [Fact]
    public async Task PlatformSuspension_RequiresAGlobalAdmin_AReason_AndRefusesARepeat()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Org Platform Suspend Rules Org");
        await ActAsEnrolledAsync(orgId, "admin");
        (await Client.PostAsJsonAsync(
                $"/identity/organizations/{orgId}/platform-suspension", new { reason = "not mine to place" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await ActAsGlobalAdminAsync();
        (await Client.DeleteAsync($"/identity/organizations/{orgId}/platform-suspension"))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "there is no suspension to lift");
        (await Client.PostAsJsonAsync($"/identity/organizations/{orgId}/platform-suspension", new { reason = " " }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "a platform suspension carries its reason");
        (await Client.PostAsJsonAsync($"/identity/organizations/{orgId}/platform-suspension", new { reason = "Abuse" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.PostAsJsonAsync($"/identity/organizations/{orgId}/platform-suspension", new { reason = "Again" }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "one suspension is already in force");
    }

    /// <summary>A member's own stream in the organization, as the SSE endpoint would register it.</summary>
    private CancellationToken OpenRealtimeStreamAsync(Guid userId, Guid orgId)
    {
        SseConnectionManager connections = Factory.Services.GetRequiredService<SseConnectionManager>();
        string connectionId = $"org-platform-{Guid.NewGuid():N}";
        connections.AddConnection(connectionId, userId.ToString(), orgId, [], [], [], clientId: null);
        return connections.GetCancellationToken(connectionId);
    }

}
