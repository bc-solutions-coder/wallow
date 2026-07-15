using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Application.Commands.RotateServiceAccountSecret;

public sealed record RotateServiceAccountSecretCommand(ServiceAccountMetadataId Id);
