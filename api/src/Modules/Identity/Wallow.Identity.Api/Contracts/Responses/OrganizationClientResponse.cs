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
    public Guid? LastRotatedByUserId { get; init; }
    public DateTimeOffset? LastRotatedAt { get; init; }

    /// <summary>When the platform operator suspended this client, or <see langword="null"/>.</summary>
    public DateTimeOffset? PlatformSuspendedAt { get; init; }

    /// <summary>The operator's reason, readable by the organization's admins but not liftable by them.</summary>
    public string? PlatformSuspensionReason { get; init; }

    /// <summary>The kind a request names, or <see langword="null"/> when it names neither.</summary>
    public static RegisteredClientKind? ParseKind(string? kind) =>
        kind switch
        {
            ApplicationKind => RegisteredClientKind.Application,
            ServiceAccountKind => RegisteredClientKind.ServiceAccount,
            _ => null,
        };

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
            LastRotatedByUserId = dto.LastRotatedByUserId,
            LastRotatedAt = dto.LastRotatedAt,
            PlatformSuspendedAt = dto.PlatformSuspendedAt,
            PlatformSuspensionReason = dto.PlatformSuspensionReason,
        };
    }
}

/// <summary>
/// The one-time reveal a registration or a secret rotation answers with: the client secret is
/// returned here and never again, and the issuer and API base URL are what the client's
/// environment must point at.
/// </summary>
public record OrganizationClientRegistrationResponse
{
    public required OrganizationClientResponse Client { get; init; }
    public required string ClientSecret { get; init; }
    public required string Issuer { get; init; }
    public required string ApiBaseUrl { get; init; }
}
