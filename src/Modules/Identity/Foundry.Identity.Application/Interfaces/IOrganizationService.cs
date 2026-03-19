using Foundry.Identity.Application.DTOs;

namespace Foundry.Identity.Application.Interfaces;

public interface IOrganizationService
{
    Task<Guid> CreateOrganizationAsync(string name, string? domain = null, string? creatorEmail = null, CancellationToken ct = default);
    Task<OrganizationDto?> GetOrganizationByIdAsync(Guid orgId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationDto>> GetOrganizationsAsync(string? search = null, int first = 0, int max = 20, CancellationToken ct = default);
    Task AddMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> GetMembersAsync(Guid orgId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationDto>> GetUserOrganizationsAsync(Guid userId, CancellationToken ct = default);
}
