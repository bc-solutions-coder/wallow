using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Interfaces;

public interface IEmailChangeRateLimiter
{
    Task<Result> CheckAsync(string userId);
}
