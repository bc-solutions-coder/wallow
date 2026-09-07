using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Tests.Common.Factories;

namespace Wallow.Identity.IntegrationTests.OrganizationClients;

[Trait("Category", "Integration")]
public sealed class TelemetryLifecycleTests(WallowApiFactory factory) : OrganizationClientsTestBase(factory)
{
    [Fact]
    public async Task RevokeWithoutGateway_RemainsPending_AndDeletionRetainsTombstone()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Telemetry lifecycle");
        await ActAsEnrolledAsync(orgId, "manager");
        (string clientId, _) = await RegisterApplicationAsync(orgId, "Telemetry lifecycle app");
        string path = $"/identity/organizations/{orgId}/clients/{clientId}";
        using HttpResponseMessage enabled = await Client.PostAsync($"{path}/observability", null);
        enabled.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement reveal = await enabled.Content.ReadFromJsonAsync<JsonElement>();
        string credential = reveal.GetProperty("configuration").GetProperty("credential").GetString()!;
        using HttpResponseMessage revoked = await Client.PostAsync($"{path}/observability/revoke", null);
        revoked.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement status = await revoked.Content.ReadFromJsonAsync<JsonElement>();
        status.GetProperty("status").GetString().Should().Be("pending-revocation");
        (await GetClientAsync(orgId, clientId)).ToString().Should().NotContain(credential);
        using HttpResponseMessage deleted = await Client.DeleteAsync(path);
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        TelemetryRegistration persisted = await db.TelemetryRegistrations.SingleAsync(e => e.ClientId == clientId);
        persisted.AccessState.Should().Be(TelemetryAccessState.Deleted);
        persisted.Revision.Should().BeGreaterThan(1);
        persisted.Status.Should().Be("pending-revocation");
    }

    [Fact]
    public async Task Lifecycle_RefusesMembersAndOtherOrganizations()
    {
        Guid owner = await OrganizationOwnedBySomeoneElseAsync("Telemetry owner");
        await ActAsEnrolledAsync(owner, "admin");
        (string clientId, _) = await RegisterApplicationAsync(owner, "Protected telemetry");
        using HttpResponseMessage enabled = await Client.PostAsync($"/identity/organizations/{owner}/clients/{clientId}/observability", null);
        enabled.EnsureSuccessStatusCode();
        await ActAsEnrolledAsync(owner, "user");
        foreach (string action in new[] { "rotate", "revoke", "disable" })
        {
            using HttpResponseMessage denied = await Client.PostAsync($"/identity/organizations/{owner}/clients/{clientId}/observability/{action}", null);
            denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        Guid other = await OrganizationOwnedBySomeoneElseAsync("Telemetry other");
        await ActAsEnrolledAsync(other, "admin");
        using HttpResponseMessage hidden = await Client.PostAsync($"/identity/organizations/{other}/clients/{clientId}/observability/rotate", null);
        hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task OrganizationDeletion_PreservesEveryTelemetryRevocation()
    {
        const string name = "Telemetry organization deletion";
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync(name);
        await ActAsEnrolledAsync(orgId, "admin");
        (string first, _) = await RegisterApplicationAsync(orgId, "Observed first");
        (string second, _) = await RegisterServiceAccountAsync(orgId, "Observed second");
        foreach (string clientId in new[] { first, second })
        {
            using HttpResponseMessage enabled = await Client.PostAsync($"/identity/organizations/{orgId}/clients/{clientId}/observability", null);
            enabled.EnsureSuccessStatusCode();
        }
        using HttpRequestMessage deletion = new(HttpMethod.Delete, $"/identity/organizations/{orgId}") { Content = JsonContent.Create(new { confirmName = name }) };
        using HttpResponseMessage deleted = await Client.SendAsync(deletion);
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        List<TelemetryRegistration> states = await db.TelemetryRegistrations.Where(e => e.OrganizationId == orgId).ToListAsync();
        states.Should().HaveCount(2);
        states.Should().OnlyContain(e => e.AccessState == TelemetryAccessState.Deleted && e.Revision == 2 && e.AcknowledgedRevision == 0);
        (await db.RegisteredClients.AnyAsync(e => e.OrganizationId == orgId)).Should().BeFalse();
    }

    [Fact]
    public async Task EnableWaitingBehindDeletion_CannotCreateOrphanedAccess()
    {
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync("Telemetry deletion race");
        await ActAsEnrolledAsync(orgId, "admin");
        (string clientId, _) = await RegisterApplicationAsync(orgId, "Concurrent telemetry");
        string path = $"/identity/organizations/{orgId}/clients/{clientId}";
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        IdentityDbContext db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await using IDbContextTransaction gate = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM identity.organizations WHERE id = {orgId} FOR UPDATE");
        await using AsyncServiceScope watchScope = Factory.Services.CreateAsyncScope();
        IdentityDbContext observer = watchScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Task<HttpResponseMessage> deleting = Client.DeleteAsync(path);
        await WaitForLockWaitersAsync(observer, 1);
        Task<HttpResponseMessage> enabling = Client.PostAsync($"{path}/observability", null);
        await WaitForLockWaitersAsync(observer, 2);
        await gate.CommitAsync();
        using HttpResponseMessage deleted = await deleting;
        using HttpResponseMessage enabled = await enabling;
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        enabled.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await db.TelemetryRegistrations.AnyAsync(e => e.ClientId == clientId && e.AccessState == TelemetryAccessState.Enabled)).Should().BeFalse();
    }

    private static async Task WaitForLockWaitersAsync(IdentityDbContext db, int expected)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        int waiters;
        do
        {
            waiters = await db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%FROM identity.organizations%FOR UPDATE%'").SingleAsync();
            if (waiters >= expected) { return; }
            await Task.Delay(20);
        } while (DateTimeOffset.UtcNow < deadline);
        waiters.Should().BeGreaterThanOrEqualTo(expected);
    }

}
