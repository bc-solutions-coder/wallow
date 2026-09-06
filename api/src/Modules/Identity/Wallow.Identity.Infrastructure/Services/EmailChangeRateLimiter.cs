using Microsoft.Extensions.Options;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Shared.Contracts.RateLimiting;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class EmailChangeRateLimiter(
    IFixedWindowCounter counter,
    IOptions<EmailChangeOptions> options) : IEmailChangeRateLimiter
{
    public async Task<Result> CheckAsync(string userId)
    {
        EmailChangeOptions limits = options.Value;
        string key = $"email:change:rate:{userId}";
        long count = await counter.IncrementAsync(key, limits.RateLimitWindow);
        if (count <= limits.RateLimitMaxRequests)
        {
            return Result.Success();
        }

        TimeSpan retryAfter = await counter.GetRetryAfterAsync(key, limits.RateLimitWindow);
        return Result.Failure(new Error(SharedErrors.RateLimitExceeded, retryAfter: retryAfter));
    }
}
