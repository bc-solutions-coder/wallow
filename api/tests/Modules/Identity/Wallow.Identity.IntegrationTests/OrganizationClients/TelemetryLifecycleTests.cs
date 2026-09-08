using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
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
    public async Task OrganizationDeletion_ConcurrentProvisioning_PreservesRevocations()
    {
        const string name = "Telemetry concurrent provisioning deletion";
        Guid orgId = await OrganizationOwnedBySomeoneElseAsync(name);
        Guid adminId = await ActAsEnrolledAsync(orgId, "admin");
        (string first, _) = await RegisterApplicationAsync(orgId, "Concurrent observed first");
        (string second, _) = await RegisterServiceAccountAsync(orgId, "Concurrent observed second");
        foreach (string clientId in new[] { first, second })
        {
            using HttpResponseMessage enabled = await Client.PostAsync($"/identity/organizations/{orgId}/clients/{clientId}/observability", null);
            enabled.EnsureSuccessStatusCode();
        }

        DeletionSaveGate gate = new(orgId);
        using WebApplicationFactory<Program> host = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.ConfigureDbContext<IdentityDbContext>(options => options.AddInterceptors(gate))));
        await using AsyncServiceScope statusScope = Factory.Services.CreateAsyncScope();
        IdentityDbContext statusDb = statusScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await statusDb.Database.OpenConnectionAsync();
        int statusPid = await statusDb.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        await using AsyncServiceScope observerScope = Factory.Services.CreateAsyncScope();
        IdentityDbContext observer = observerScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Task reconciliation = Task.CompletedTask;
        HttpResponseMessage deletionResponse;
        Task<HttpResponseMessage> deleting = DeleteOrganizationAsync(() => host.CreateClient(), adminId, orgId, name);
        try
        {
            await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            TelemetryRegistration snapshot = await statusDb.TelemetryRegistrations.AsTracking().SingleAsync(e => e.ClientId == first);
            TelemetryProvisioner provisioner = new(statusDb, statusScope.ServiceProvider.GetRequiredService<IHttpClientFactory>(),
                new ConfigurationBuilder().Build(), TimeProvider.System);
            reconciliation = provisioner.ReconcileAsync(snapshot.Id.Value, CancellationToken.None);
            await WaitForAsync(async () => reconciliation.IsCompleted || await observer.Database.SqlQuery<int>(
                $"SELECT count(*)::integer AS \"Value\" FROM pg_stat_activity WHERE pid = {statusPid} AND wait_event_type = 'Lock'").SingleAsync() == 1);
        }
        finally
        {
            gate.Release.TrySetResult(true);
            deletionResponse = await deleting;
            await reconciliation;
        }
        using HttpResponseMessage deleted = deletionResponse;
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());
        List<TelemetryRegistration> states = await observer.TelemetryRegistrations.Where(e => e.OrganizationId == orgId).ToListAsync();
        states.Should().HaveCount(2);
        states.Should().OnlyContain(e => e.AccessState == TelemetryAccessState.Deleted && e.Revision == 2 && e.AcknowledgedRevision == 0);
        (await observer.RegisteredClients.AnyAsync(e => e.OrganizationId == orgId)).Should().BeFalse();
    }

    private static async Task<HttpResponseMessage> DeleteOrganizationAsync(Func<HttpClient> createClient, Guid adminId, Guid organizationId, string name)
    {
        using HttpClient client = createClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer test-token");
        client.DefaultRequestHeaders.Add("X-Test-User-Id", adminId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Roles", "admin");
        client.DefaultRequestHeaders.Add("X-Test-Tenant-Id", organizationId.ToString("D"));
        using HttpRequestMessage request = new(HttpMethod.Delete, $"/identity/organizations/{organizationId}")
        {
            Content = JsonContent.Create(new { confirmName = name }),
        };
        return await client.SendAsync(request);
    }

    private sealed class DeletionSaveGate(Guid organizationId) : SaveChangesInterceptor
    {
        public TaskCompletionSource<bool> Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is IdentityDbContext db && db.ChangeTracker.Entries<TelemetryRegistration>()
                .Any(e => e.Entity.OrganizationId == organizationId && e.Entity.AccessState == TelemetryAccessState.Deleted))
            {
                Reached.TrySetResult(true);
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
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
