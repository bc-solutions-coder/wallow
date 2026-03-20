namespace Wallow.Shared.Contracts.Identity;

public interface IUserQueryService
{
    Task<string> GetUserEmailAsync(Guid userId, CancellationToken ct = default);
    Task<int> GetNewUsersCountAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<int> GetActiveUsersCountAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> GetTotalUsersCountAsync(Guid tenantId, CancellationToken ct = default);
}
