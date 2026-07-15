using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Application.Commands.RevokeServiceAccount;

public sealed record RevokeServiceAccountCommand(ServiceAccountMetadataId Id);
