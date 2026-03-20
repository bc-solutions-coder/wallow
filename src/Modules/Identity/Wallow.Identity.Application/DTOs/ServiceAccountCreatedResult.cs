using Wallow.Identity.Domain.Identity;

namespace Wallow.Identity.Application.DTOs;

public record ServiceAccountCreatedResult(
    ServiceAccountMetadataId Id,
    string ClientId,
    string ClientSecret,
    string TokenEndpoint,
    IReadOnlyList<string> Scopes);
