using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Api.Contracts.Responses;

public record OrganizationClientResponse
{
    public const string ApplicationKind = "application";
    public const string ServiceAccountKind = "service-account";

    public required string ClientId { get; init; }
    public required string Name { get; init; }

    /// <summary><c>application</c> or <c>service-account</c>.</summary>
    public required string Kind { get; init; }

    /// <summary><c>active</c> or <c>suspended</c>.</summary>
    public required string Status { get; init; }
    public required IReadOnlyList<string> RedirectUris { get; init; }
    public required IReadOnlyList<string> PostLogoutRedirectUris { get; init; }
    public string? BackchannelLogoutUri { get; init; }
    public required IReadOnlyList<string> Scopes { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }

    public static OrganizationClientResponse From(OrganizationClientDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new OrganizationClientResponse
        {
            ClientId = dto.ClientId,
            Name = dto.Name,
            Kind = dto.Kind == RegisteredClientKind.ServiceAccount ? ServiceAccountKind : ApplicationKind,
            Status = dto.Status == RegisteredClientStatus.Suspended ? "suspended" : "active",
            RedirectUris = dto.RedirectUris,
            PostLogoutRedirectUris = dto.PostLogoutRedirectUris,
            BackchannelLogoutUri = dto.BackchannelLogoutUri,
            Scopes = dto.Scopes,
            CreatedByUserId = dto.CreatedByUserId,
            CreatedAt = dto.CreatedAt,
            LastUsedAt = dto.LastUsedAt,
        };
    }
}

/// <summary>
/// The one-time registration reveal: the client secret is returned here and never again, and the
/// issuer and API base URL are what the application's environment must point at.
/// </summary>
public record OrganizationClientRegistrationResponse
{
    public required OrganizationClientResponse Client { get; init; }
    public required string ClientSecret { get; init; }
    public required string Issuer { get; init; }
    public required string ApiBaseUrl { get; init; }
}
