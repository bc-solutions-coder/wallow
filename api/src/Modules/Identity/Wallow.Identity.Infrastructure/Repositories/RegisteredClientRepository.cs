using Microsoft.EntityFrameworkCore;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Infrastructure.Persistence;

namespace Wallow.Identity.Infrastructure.Repositories;

public sealed class RegisteredClientRepository(IdentityDbContext context) : IRegisteredClientRepository
{
    public Task<RegisteredClient?> GetByClientIdAsync(string clientId, CancellationToken ct = default) =>
        context.RegisteredClients.AsTracking().FirstOrDefaultAsync(c => c.ClientId == clientId, ct);

    public async Task<IReadOnlyList<RegisteredClient>> ListByOrganizationAsync(
        Guid organizationId,
        CancellationToken ct = default) =>
        await context.RegisteredClients
            .Where(c => c.OrganizationId == organizationId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public void Add(RegisteredClient client) => context.RegisteredClients.Add(client);

    public void Remove(RegisteredClient client) => context.RegisteredClients.Remove(client);

    public Task SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}
