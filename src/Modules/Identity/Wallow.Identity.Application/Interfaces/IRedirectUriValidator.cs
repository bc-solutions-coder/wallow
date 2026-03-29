namespace Wallow.Identity.Application.Interfaces;

public interface IRedirectUriValidator
{
    Task<bool> IsAllowedAsync(string uri, CancellationToken ct = default);
}
