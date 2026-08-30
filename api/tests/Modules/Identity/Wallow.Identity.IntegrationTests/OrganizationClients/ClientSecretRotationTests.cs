using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Wallow.Identity.IntegrationTests.OAuth2;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OrganizationClients;

/// <summary>
/// Secret rotation on the org-scoped client surface: the new secret is revealed once and works
/// immediately, the old one stops working at the same moment, and the optional
/// <c>revokeActiveTokens</c> flag ends every token the client was already issued. Proven against
/// the real token endpoint and, for bearer calls, the real validation handler (the stub scheme is
/// bypassed per request).
/// </summary>
[Trait("Category", "Integration")]
public class ClientSecretRotationTests(WallowApiFactory factory) : OrganizationClientsTestBase(factory)
{
    [Fact]
    public async Task Rotate_RevealsANewSecretOnce_AndTheOldOneStopsWorkingImmediately()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Rotation Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, string oldSecret) = await RegisterServiceAccountAsync(orgId, "Nightly sync");
        (await ClientCredentialsAsync(clientId, oldSecret)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await Client.PostAsJsonAsync(
            $"/identity/organizations/{orgId}/clients/{clientId}/rotate-secret",
            new { revokeActiveTokens = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        string newSecret = body.GetProperty("clientSecret").GetString()!;
        newSecret.Should().NotBeNullOrEmpty().And.NotBe(oldSecret);
        body.GetProperty("client").GetProperty("clientId").GetString().Should().Be(clientId);
        body.GetProperty("issuer").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("apiBaseUrl").GetString().Should().NotBeNullOrEmpty();

        (await ClientCredentialsAsync(clientId, oldSecret)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await ClientCredentialsAsync(clientId, newSecret)).StatusCode.Should().Be(HttpStatusCode.OK);

        // The reveal is one-time: no read path carries the secret afterwards.
        HttpResponseMessage read = await Client.GetAsync($"/identity/organizations/{orgId}/clients/{clientId}");
        string readBody = await read.Content.ReadAsStringAsync();
        readBody.Should().NotContain(newSecret).And.NotContain("clientSecret");
    }

    [Fact]
    public async Task Rotate_WithoutTheFlag_LeavesAnApplicationsRefreshTokenWorking()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Refresh Org");
        string managerEmail = await ActAsEnrolledEmailAsync(orgId, "manager");
        (string clientId, string oldSecret) = await RegisterApplicationAsync(orgId, "Dashboard");
        TokenOutcome tokens = await SignInThroughAsync(managerEmail, clientId, oldSecret);

        string newSecret = await RotateAsync(orgId, clientId, revokeActiveTokens: false);

        TokenOutcome refreshed = await Harness.RefreshAsync(clientId, newSecret, tokens.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK, refreshed.Body);
        refreshed.AccessToken.Should().NotBeNullOrEmpty();

        // The old secret is gone even for a refresh the grant would otherwise have allowed.
        TokenOutcome withOldSecret = await Harness.RefreshAsync(clientId, oldSecret, refreshed.RefreshToken!);
        withOldSecret.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        withOldSecret.Error.Should().Be("invalid_client");
    }

    [Fact]
    public async Task Rotate_WithTheFlag_EndsEveryTokenTheClientWasIssued()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Revoke Org");
        string managerEmail = await ActAsEnrolledEmailAsync(orgId, "manager");
        (string clientId, string oldSecret) = await RegisterApplicationAsync(orgId, "Dashboard");
        TokenOutcome tokens = await SignInThroughAsync(managerEmail, clientId, oldSecret);

        string newSecret = await RotateAsync(orgId, clientId, revokeActiveTokens: true);

        TokenOutcome refreshed = await Harness.RefreshAsync(clientId, newSecret, tokens.RefreshToken!);
        refreshed.StatusCode.Should().Be(HttpStatusCode.BadRequest, refreshed.Body);
        refreshed.Error.Should().Be("invalid_grant");
    }

    [Fact]
    public async Task Rotate_WithTheFlag_RejectsAServiceAccountsOutstandingBearerToken()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Bearer Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, string oldSecret) = await RegisterServiceAccountAsync(orgId, "Nightly sync");
        HttpResponseMessage minted = await ClientCredentialsAsync(clientId, oldSecret);
        string accessToken = (await minted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;
        (await BearerCallAsync(accessToken)).StatusCode.Should().Be(HttpStatusCode.OK, "the token is good until it is revoked");

        await RotateAsync(orgId, clientId, revokeActiveTokens: true);

        (await BearerCallAsync(accessToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rotate_WithoutTheFlag_LeavesAServiceAccountsOutstandingBearerTokenWorking()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Keep Bearer Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, string oldSecret) = await RegisterServiceAccountAsync(orgId, "Nightly sync");
        HttpResponseMessage minted = await ClientCredentialsAsync(clientId, oldSecret);
        string accessToken = (await minted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()!;

        await RotateAsync(orgId, clientId, revokeActiveTokens: false);

        (await BearerCallAsync(accessToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Rotate_RecordsWhoRotatedAndWhen_OnTheClient()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Provenance Org");
        Guid actor = await ActAsEnrolledAsync(orgId, "admin");
        (string clientId, _) = await RegisterServiceAccountAsync(orgId, "Nightly sync");

        JsonElement before = await GetClientAsync(orgId, clientId);
        before.GetProperty("createdByUserId").GetGuid().Should().Be(actor);
        before.GetProperty("lastRotatedByUserId").ValueKind.Should().Be(JsonValueKind.Null);
        before.GetProperty("lastRotatedAt").ValueKind.Should().Be(JsonValueKind.Null);

        await RotateAsync(orgId, clientId, revokeActiveTokens: false);

        JsonElement after = await GetClientAsync(orgId, clientId);
        after.GetProperty("lastRotatedByUserId").GetGuid().Should().Be(actor);
        after.GetProperty("lastRotatedAt").GetDateTimeOffset().Should().BeAfter(before.GetProperty("createdAt").GetDateTimeOffset().AddSeconds(-1));
    }

    [Fact]
    public async Task RegisterAndRotate_AreAudited_WithActorOrganizationAndClient()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Audit Org");
        Guid actor = await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, _) = await RegisterServiceAccountAsync(orgId, "Nightly sync");
        await RotateAsync(orgId, clientId, revokeActiveTokens: true);

        AuthAuditEntry registered = await AuditRowAsync("ClientRegistered", clientId);
        registered.ActorId.Should().Be(actor);
        registered.UserId.Should().Be(actor);
        registered.TenantId.Should().Be(orgId);

        AuthAuditEntry rotated = await AuditRowAsync("ClientSecretRotated", clientId);
        rotated.ActorId.Should().Be(actor);
        rotated.TenantId.Should().Be(orgId);
    }

    [Fact]
    public async Task Rotate_AnswersNotFound_ForAnotherOrganizationsClient_AndForbidsAMember()
    {
        Guid ownerOrg = await OrganizationOwnedBySomeoneElseAsync("Rotation Owner Org");
        await ActAsEnrolledAsync(ownerOrg, "manager");
        (string clientId, _) = await RegisterServiceAccountAsync(ownerOrg, "Nightly sync");

        Guid otherOrg = await OrganizationOwnedBySomeoneElseAsync("Rotation Other Org");
        await ActAsEnrolledAsync(otherOrg, "manager");
        HttpResponseMessage crossOrg = await Client.PostAsJsonAsync(
            $"/identity/organizations/{otherOrg}/clients/{clientId}/rotate-secret",
            new { revokeActiveTokens = false });
        crossOrg.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await ActAsEnrolledAsync(ownerOrg, "user");
        HttpResponseMessage asMember = await Client.PostAsJsonAsync(
            $"/identity/organizations/{ownerOrg}/clients/{clientId}/rotate-secret",
            new { revokeActiveTokens = false });
        asMember.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
