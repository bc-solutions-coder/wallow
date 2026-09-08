using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Errors;
using Wallow.Identity.Domain.Identity;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Identity.Infrastructure.Services;

internal static class TelemetryOwnership
{
    public static async Task LockOrganizationAsync(IdentityDbContext db, Guid organizationId, CancellationToken ct)
    {
        if (db.Database.IsRelational())
        {
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM identity.organizations WHERE id = {organizationId} FOR UPDATE", ct);
        }
    }

    public static async Task LockRegistrationAsync(IdentityDbContext db, RegisteredClientId id, CancellationToken ct)
    {
        if (db.Database.IsRelational())
        {
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM identity.telemetry_registrations WHERE id = {id.Value} FOR UPDATE", ct);
        }
    }

    public static async Task LockOrganizationRegistrationsAsync(IdentityDbContext db, Guid organizationId, CancellationToken ct)
    {
        if (db.Database.IsRelational())
        {
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM identity.telemetry_registrations WHERE organization_id = {organizationId} ORDER BY id FOR UPDATE", ct);
        }
    }

    public static async Task RequireOrganizationAsync(IdentityDbContext db, Guid organizationId, CancellationToken ct)
    {
        await LockOrganizationAsync(db, organizationId, ct);
        OrganizationId id = OrganizationId.Create(organizationId);
        if (!await db.Organizations.IgnoreQueryFilters().AnyAsync(e => e.Id == id, ct))
        {
            throw new EntityNotFoundException(IdentityErrors.OrganizationNotFound, organizationId);
        }
    }

    public static async Task RequireClientAsync(IdentityDbContext db, RegisteredClient client, CancellationToken ct)
    {
        await RequireOrganizationAsync(db, client.OrganizationId, ct);
        if (!await db.RegisteredClients.AnyAsync(e => e.Id == client.Id && e.OrganizationId == client.OrganizationId, ct))
        {
            throw new EntityNotFoundException(IdentityErrors.TelemetryClientNotFound, client.Id);
        }
    }
}
