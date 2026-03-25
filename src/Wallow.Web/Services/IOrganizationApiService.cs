using Wallow.Web.Models;

namespace Wallow.Web.Services;

public interface IOrganizationApiService
{
    Task<List<OrganizationModel>> GetOrganizationsAsync(CancellationToken ct = default);
    Task<OrganizationModel?> GetOrganizationAsync(Guid orgId, CancellationToken ct = default);
    Task<List<OrganizationMemberModel>> GetMembersAsync(Guid orgId, CancellationToken ct = default);
    Task<List<ClientModel>> GetClientsByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
