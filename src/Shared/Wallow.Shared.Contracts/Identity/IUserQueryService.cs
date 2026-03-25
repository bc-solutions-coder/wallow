namespace Wallow.Shared.Contracts.Identity;

public interface IUserQueryService
{
    Task<string> GetUserEmailAsync(Guid userId, CancellationToken ct = default);
    Task<int> GetNewUsersCountAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default);
    Task<int> GetActiveUsersCountAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> GetTotalUsersCountAsync(Guid tenantId, CancellationToken ct = default);
    Task<UserSearchPageResult> SearchUsersAsync(Guid tenantId, string? search, int skip, int take, CancellationToken ct = default);
}

public record UserSearchItem(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IReadOnlyList<string> Roles);

public record UserSearchPageResult(
    IReadOnlyList<UserSearchItem> Items,
    int TotalCount,
    int Page,
    int PageSize);
