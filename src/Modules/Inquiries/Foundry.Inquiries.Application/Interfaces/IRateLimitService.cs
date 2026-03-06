namespace Foundry.Inquiries.Application.Interfaces;

public interface IRateLimitService
{
    Task<bool> IsAllowedAsync(string key, CancellationToken cancellationToken = default);
}
