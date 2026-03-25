namespace Wallow.Web.Models;

public sealed record RegisterAppModel(
    string DisplayName,
    string ClientType,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> Scopes);
