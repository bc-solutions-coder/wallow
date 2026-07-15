namespace Wallow.Web.Models;

public sealed record AppModel(
    string ClientId,
    string DisplayName,
    string ClientType,
    IReadOnlyList<string> RedirectUris,
    DateTimeOffset? CreatedAt);
