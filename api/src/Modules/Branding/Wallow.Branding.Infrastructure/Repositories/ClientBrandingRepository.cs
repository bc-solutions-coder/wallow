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
    /// Reads globally unique client branding without tenant filters, including anonymous reads.
    /// Write callers must verify organization ownership through the client directory.
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
        // Save before enqueueing so rejected writes publish nothing. The event and rows commit
        // together, then delivery flushes. Ambiguous-commit retries can redeliver; consumers
        // must be idempotent.
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
            // Detach stale branding entries so callers can recover without handling EF exceptions.
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
            // Detach losing inserts so callers can re-fetch the winner and retry as an update.
            List<EntityEntry<ClientBranding>> losingInserts = context.ChangeTracker
                .Entries<ClientBranding>()
                .Where(e => e.State == EntityState.Added)
                .ToList();
            if (losingInserts.Count == 0)
            {
                // Without a pending insert, the retry-as-update exception contract does not apply.
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
