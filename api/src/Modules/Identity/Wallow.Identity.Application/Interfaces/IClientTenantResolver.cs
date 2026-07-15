namespace Wallow.Identity.Application.Interfaces;

public record ClientTenantInfo(Guid TenantId, string? TenantName);

public interface IClientTenantResolver
{
    Task<ClientTenantInfo?> ResolveAsync(string clientId, CancellationToken ct = default);
}
