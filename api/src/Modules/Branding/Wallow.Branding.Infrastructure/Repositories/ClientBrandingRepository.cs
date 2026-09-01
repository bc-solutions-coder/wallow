using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Wallow.Branding.Application.Exceptions;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Branding.Infrastructure.Persistence;
using Wallow.Shared.Contracts;
using Wallow.Shared.Kernel.Identity;
using Wolverine.EntityFrameworkCore;

namespace Wallow.Branding.Infrastructure.Repositories;

public sealed class ClientBrandingRepository(
    BrandingDbContext context,
    IDbContextOutbox outbox) : IClientBrandingRepository
{
    /// <summary>
    /// Resolves branding by client ID, bypassing tenant query filters (IgnoreQueryFilters).
    /// client_id carries a repo-wide unique index, so no tenant partitions this table and the
    /// parameter is itself the selector. The public read is anonymous, so no tenant resolves at
    /// all; the writes are authorized on the OIDC application's creatorUserId — which OpenIddict
    /// stores globally — rather than on the ambient tenant, and a filtered miss would send
    /// UpsertBranding down its insert branch and into that unique index.
    /// </summary>
    public Task<ClientBranding?> GetByClientIdAsync(string clientId, CancellationToken ct = default)
    {
        return context.ClientBrandings
            .AsTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.ClientId == clientId, ct);
    }

    public Task<string?> FindDisplayNameAsync(string clientId, CancellationToken ct = default)
    {
        return context.ClientBrandings
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(b => b.ClientId == clientId)
            .Select(b => (string?)b.DisplayName)
            .FirstOrDefaultAsync(ct);
    }

    public void UseTenant(TenantId tenantId)
    {
        context.SetTenant(tenantId);
    }

    public async Task<IReadOnlyList<ClientBranding>> ListAsync(CancellationToken ct = default)
    {
        return await context.ClientBrandings
            .AsTracking()
            .ToListAsync(ct);
    }

    public void Add(ClientBranding branding)
    {
        context.ClientBrandings.Add(branding);
    }

    public void Remove(ClientBranding branding)
    {
        context.ClientBrandings.Remove(branding);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        SaveWithTypedExceptionsAsync(() => context.SaveChangesAsync(ct));

    public async Task SaveChangesAndPublishAsync(IIntegrationEvent @event, CancellationToken ct = default)
    {
        // Same pattern as Identity's OrganizationService.DeleteOrganizationAsync: the event is
        // published through the enrolled outbox INSIDE the transaction — its envelope commits or
        // rolls back with the rows — and only flushed to subscribers after the commit, so a crash
        // between the save and the publish no longer drops the event. The save comes first: a
        // rejected write (unique violation, vanished row) must publish nothing, so the caller's
        // retry can pass the same event again. Redelivery after an ambiguous commit is possible
        // (the retrying execution strategy reruns the delegate), so consumers stay idempotent.
        outbox.Enroll(context);
        await SaveWithTypedExceptionsAsync(async () =>
        {
            IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(
                ct,
                async token =>
                {
                    await using IDbContextTransaction transaction =
                        await context.Database.BeginTransactionAsync(token);
                    await context.SaveChangesAsync(token);
                    await outbox.PublishAsync(@event);
                    await transaction.CommitAsync(token);
                });
        });
        await outbox.FlushOutgoingMessagesAsync();
    }

    private async Task SaveWithTypedExceptionsAsync(Func<Task> save)
    {
        try
        {
            await save();
        }
        catch (DbUpdateConcurrencyException ex) when (ex.Entries.Any(e => e.Entity is ClientBranding))
        {
            // The row being written was deleted underneath the save — a client deletion removing
            // branding while another caller updates it. Detach the stale entries so the context
            // stays usable and surface the loss typed; the Api layer never sniffs EF exceptions.
            ClientBranding firstStale = (ClientBranding)ex.Entries.First(e => e.Entity is ClientBranding).Entity;
            foreach (EntityEntry entry in ex.Entries)
            {
                if (entry.Entity is ClientBranding)
                {
                    entry.State = EntityState.Detached;
                }
            }

            throw new ClientBrandingConcurrentlyDeletedException(firstStale.ClientId, ex);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent writer (ClientRegisteredHandler consuming the registration event, or a
            // racing PUT) inserted the row between the caller's existence check and this save.
            // Detach the losing inserts so the caller can re-fetch the winner and apply its write
            // as an update on this same repository.
            List<EntityEntry<ClientBranding>> losingInserts = context.ChangeTracker
                .Entries<ClientBranding>()
                .Where(e => e.State == EntityState.Added)
                .ToList();
            if (losingInserts.Count == 0)
            {
                // Nothing to detach means the typed exception's retry-as-update contract cannot
                // hold; let the original failure speak for itself.
                throw;
            }

            foreach (EntityEntry<ClientBranding> entry in losingInserts)
            {
                entry.State = EntityState.Detached;
            }

            throw new DuplicateClientBrandingException(losingInserts[0].Entity.ClientId, ex);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
