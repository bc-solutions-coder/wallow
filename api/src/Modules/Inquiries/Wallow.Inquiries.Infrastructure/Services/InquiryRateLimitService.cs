using Wallow.Inquiries.Application.Interfaces;
using Wallow.Shared.Contracts.RateLimiting;

namespace Wallow.Inquiries.Infrastructure.Services;

public class InquiryRateLimitService(IFixedWindowCounter counter) : IRateLimitService
{
    private const int MaxRequests = 5;
    private static readonly TimeSpan _window = TimeSpan.FromMinutes(15);

    public async Task<bool> IsAllowedAsync(string key, CancellationToken cancellationToken = default)
    {
        long count = await counter.IncrementAsync(key, _window);

        return count <= MaxRequests;
    }
}
