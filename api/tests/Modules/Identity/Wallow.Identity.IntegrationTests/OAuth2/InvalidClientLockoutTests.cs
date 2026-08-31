using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.IntegrationTests.OrganizationClients;
using Wallow.Shared.Infrastructure.Core.Auditing;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OAuth2;

/// <summary>
/// Pins the #150 invalid_client requirements against the real token endpoint: every failed
/// client authentication lands a userless <c>ClientAuthenticationFailed</c> audit row, a client
/// that fails often enough is temporarily rejected even when it finally presents the correct
/// secret, and the lockout is per client — a neighbour with one slip is unaffected.
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

        // At the threshold the client is locked: the CORRECT secret is refused too, with the
        // same invalid_client answer a wrong secret gets — the lockout discloses nothing.
        using HttpResponseMessage lockedOut = await ClientCredentialsAsync(lockedId, lockedSecret);
        lockedOut.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a locked-out client must be rejected even with the correct secret");
        (await lockedOut.Content.ReadAsStringAsync()).Should().Contain("invalid_client");
        (await AuditRowCountAsync("ClientAuthenticationFailed", lockedId)).Should().Be(
            _lockoutOptions.FailureThreshold,
            "the lockout's own refusal is not a failed authentication and must be neither audited nor counted");

        // One bad attempt on the neighbour is audited but nowhere near the threshold: its
        // correct secret keeps working — the counter partitions per client_id.
        using HttpResponseMessage neighbourRefused = await ClientCredentialsAsync(neighbourId, "wrong-secret");
        neighbourRefused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await AuditRowAsync("ClientAuthenticationFailed", neighbourId);

        using HttpResponseMessage neighbourAllowed = await ClientCredentialsAsync(neighbourId, neighbourSecret);
        neighbourAllowed.StatusCode.Should().Be(HttpStatusCode.OK,
            "one failure on a neighbouring client must not trip its lockout");
    }

    /// <summary>
    /// A straight count, no waiting: the audit handler runs inside the token request, so every
    /// row from an already-answered attempt is committed before this queries.
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
