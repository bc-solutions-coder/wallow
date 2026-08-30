using Wallow.Identity.Domain.Entities;

namespace Wallow.Identity.Application.Interfaces;

public interface IRegisteredClientRepository
{
    Task<RegisteredClient?> GetByClientIdAsync(string clientId, CancellationToken ct = default);
    Task<IReadOnlyList<RegisteredClient>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default);
    void Add(RegisteredClient client);
    void Remove(RegisteredClient client);
    Task SaveChangesAsync(CancellationToken ct = default);
}
