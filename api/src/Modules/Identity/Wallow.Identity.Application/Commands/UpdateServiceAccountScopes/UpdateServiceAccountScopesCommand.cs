using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Application.Commands.UpdateServiceAccountScopes;

public sealed record UpdateServiceAccountScopesCommand(
    ServiceAccountMetadataId Id,
    IEnumerable<string> Scopes);
