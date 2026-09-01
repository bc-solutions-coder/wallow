using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Npgsql;
using Wallow.Branding.Application.Exceptions;
using Wallow.Branding.Application.Interfaces;
using Wallow.Branding.Domain.Entities;
using Wallow.Branding.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Branding.Infrastructure.Repositories;

public sealed class ClientBrandingRepository(BrandingDbContext context) : IClientBrandingRepository
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

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await context.SaveChangesAsync(ct);
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
