namespace Wallow.Identity.Application.DTOs;

public sealed record DeveloperAppInfo(
    string ClientId,
    string DisplayName,
    string ClientType,
    IReadOnlyList<string> RedirectUris,
    DateTimeOffset? CreatedAt);
