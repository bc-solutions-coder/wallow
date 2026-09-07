using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.IntegrationTests.OrganizationClients;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Checks failed-client authentication audits and per-client lockout at the token endpoint.
/// The locked client is refused with its correct secret while another client remains usable.
/// </summary>
[Trait("Category", "Integration")]
public class InvalidClientLockoutTests(WallowApiFactory factory) : OrganizationClientsTestBase(factory)
{
    private static readonly InvalidClientLockoutOptions _lockoutOptions = new();

    [Fact]
    public async Task RepeatedBadSecrets_AuditAndLockTheClient_WithoutTouchingItsNeighbour()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Lockout Org");
        await ActAsEnrolledAsync(orgId, "manager");
        (string lockedId, string lockedSecret) = await RegisterServiceAccountAsync(orgId, "Locked-out sync");
        (string neighbourId, string neighbourSecret) = await RegisterServiceAccountAsync(orgId, "Neighbour sync");

        for (int attempt = 0; attempt < _lockoutOptions.FailureThreshold; attempt++)
        {
            using HttpResponseMessage refused = await ClientCredentialsAsync(lockedId, "wrong-secret");
            refused.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
                "attempt {0} presents a wrong secret", attempt + 1);
        }

        AuthAuditEntry audited = await AuditRowAsync("ClientAuthenticationFailed", lockedId);
        audited.UserId.Should().BeNull("a failed client authentication has no user");
        (await AuditRowCountAsync("ClientAuthenticationFailed", lockedId)).Should().Be(
            _lockoutOptions.FailureThreshold, "every failed attempt is audited, not just the first");

        // Lockout must return invalid_client even for the correct secret.
        using HttpResponseMessage lockedOut = await ClientCredentialsAsync(lockedId, lockedSecret);
        lockedOut.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a locked-out client must be rejected even with the correct secret");
        (await lockedOut.Content.ReadAsStringAsync()).Should().Contain("invalid_client");
        (await AuditRowCountAsync("ClientAuthenticationFailed", lockedId)).Should().Be(
            _lockoutOptions.FailureThreshold,
            "the lockout's own refusal is not a failed authentication and must be neither audited nor counted");

        // One failure on another client must not inherit this client lockout.
        using HttpResponseMessage neighbourRefused = await ClientCredentialsAsync(neighbourId, "wrong-secret");
        neighbourRefused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuditRowAsync("ClientAuthenticationFailed", neighbourId);

        using HttpResponseMessage neighbourAllowed = await ClientCredentialsAsync(neighbourId, neighbourSecret);
        neighbourAllowed.StatusCode.Should().Be(HttpStatusCode.OK,
            "one failure on a neighbouring client must not trip its lockout");
    }

    /// <summary>
    /// Counts audit rows immediately after the completed requests.
    /// </summary>
    private async Task<int> AuditRowCountAsync(string eventType, string clientId)
    {
        IDbContextFactory<AuthAuditDbContext> contexts =
            Factory.Services.GetRequiredService<IDbContextFactory<AuthAuditDbContext>>();
        await using AuthAuditDbContext context = await contexts.CreateDbContextAsync();
        return await context.AuthAuditEntries
            .CountAsync(e => e.EventType == eventType && e.ClientId == clientId);
    }
}
