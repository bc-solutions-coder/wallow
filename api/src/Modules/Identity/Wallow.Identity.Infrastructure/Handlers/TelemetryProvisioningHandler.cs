using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Identity.Infrastructure.Services;
using Wallow.Shared.Contracts.Identity.Events;

namespace Wallow.Identity.Infrastructure.Handlers;

public sealed class TelemetryProvisioningHandler(TelemetryProvisioner provisioner, IdentityDbContext db)
{
    public Task HandleAsync(TelemetryDesiredChangedEvent message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        return provisioner.ReconcileAsync(message.RegistrationId, ct);
    }

    public async Task HandleAsync(ClientRegisteredEvent message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        TelemetryRegistration? state = await db.TelemetryRegistrations.FirstOrDefaultAsync(e => e.ClientId == message.ClientId, ct);
        if (state is not null)
        {
            await provisioner.ReconcileAsync(state.Id.Value, ct);
        }
    }
    public async Task HandleAsync(ClientDeletedEvent message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        List<TelemetryRegistration> states = await db.TelemetryRegistrations.Where(e => e.ClientId == message.ClientId).ToListAsync(ct);
        foreach (TelemetryRegistration state in states) { await provisioner.ReconcileAsync(state.Id.Value, ct); }
    }

    public async Task HandleAsync(OrganizationDeletedEvent message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        List<TelemetryRegistration> states = await db.TelemetryRegistrations.Where(e => e.OrganizationId == message.OrganizationId).ToListAsync(ct);
        foreach (TelemetryRegistration state in states) { await provisioner.ReconcileAsync(state.Id.Value, ct); }
    }

}
