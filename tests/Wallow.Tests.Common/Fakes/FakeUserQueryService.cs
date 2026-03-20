using Wallow.Shared.Contracts.Identity;

namespace Wallow.Tests.Common.Fakes;

public sealed class FakeUserQueryService : IUserQueryService
{
    public Task<string> GetUserEmailAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult("test@example.com");

    public Task<int> GetNewUsersCountAsync(Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default)
        => Task.FromResult(0);

    public Task<int> GetActiveUsersCountAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(0);

    public Task<int> GetTotalUsersCountAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(0);
}
