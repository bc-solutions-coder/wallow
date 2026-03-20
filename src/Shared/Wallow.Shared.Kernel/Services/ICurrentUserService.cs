namespace Wallow.Shared.Kernel.Services;

public interface ICurrentUserService
{
    Guid? GetCurrentUserId();
    Guid? UserId => GetCurrentUserId();
}
