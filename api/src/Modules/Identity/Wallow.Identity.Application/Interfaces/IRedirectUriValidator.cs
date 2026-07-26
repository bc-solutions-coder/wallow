namespace Wallow.Identity.Application.Interfaces;

public interface IRedirectUriValidator
{
    Task<bool> IsAllowedAsync(string uri, string? clientId = null, CancellationToken ct = default);
}
