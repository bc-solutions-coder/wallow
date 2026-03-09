// ReSharper disable UnusedAutoPropertyAccessor.Global
using System.Text.Json.Serialization;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Domain.Identity;

namespace Foundry.Identity.Application.DTOs;

/// <summary>
/// SCIM configuration details for a tenant.
/// </summary>
public record ScimConfigurationDto(
    bool IsEnabled,
    string? TokenPrefix,
    DateTime? TokenExpiresAt,
    DateTime? LastSyncAt,
    string ScimEndpointUrl,
    bool AutoActivateUsers,
    string? DefaultRole,
    bool DeprovisionOnDelete);

/// <summary>
/// Request to enable SCIM provisioning.
/// </summary>
public record EnableScimRequest(
    bool AutoActivateUsers = true,
    string? DefaultRole = null,
    bool DeprovisionOnDelete = false);

/// <summary>
/// Response from enabling SCIM. Contains the plaintext token only when a new configuration is created.
/// </summary>
public record EnableScimResponse(ScimConfigurationDto Configuration, string? PlainTextToken);

/// <summary>
/// SCIM 2.0 User resource representation.
/// </summary>
public record ScimUser
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string> Schemas { get; init; } = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" };

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }

    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public ScimName? Name { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("emails")]
    public IReadOnlyList<ScimEmail>? Emails { get; init; }

    [JsonPropertyName("active")]
    public bool Active { get; init; }

    [JsonPropertyName("groups")]
    public IReadOnlyList<ScimGroupReference>? Groups { get; init; }

    [JsonPropertyName("meta")]
    public ScimMeta? Meta { get; init; }
}

/// <summary>
/// SCIM name component.
/// </summary>
public record ScimName
{
    [JsonPropertyName("formatted")]
    public string? Formatted { get; init; }

    [JsonPropertyName("familyName")]
    public string? FamilyName { get; init; }

    [JsonPropertyName("givenName")]
    public string? GivenName { get; init; }

    [JsonPropertyName("middleName")]
    public string? MiddleName { get; init; }
}

/// <summary>
/// SCIM email component.
/// </summary>
public record ScimEmail
{
    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("primary")]
    public bool Primary { get; init; }
}

/// <summary>
/// SCIM group reference within a user.
/// </summary>
public record ScimGroupReference
{
    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    [JsonPropertyName("$ref")]
    public string? Ref { get; init; }

    [JsonPropertyName("display")]
    public string? Display { get; init; }
}

/// <summary>
/// SCIM resource metadata.
/// </summary>
public record ScimMeta
{
    [JsonPropertyName("resourceType")]
    public string ResourceType { get; init; } = string.Empty;

    [JsonPropertyName("created")]
    public DateTime? Created { get; init; }

    [JsonPropertyName("lastModified")]
    public DateTime? LastModified { get; init; }

    [JsonPropertyName("location")]
    public string? Location { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }
}

/// <summary>
/// SCIM 2.0 Group resource representation.
/// </summary>
public record ScimGroup
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string> Schemas { get; init; } = new[] { "urn:ietf:params:scim:schemas:core:2.0:Group" };

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("members")]
    public IReadOnlyList<ScimMember>? Members { get; init; }

    [JsonPropertyName("meta")]
    public ScimMeta? Meta { get; init; }
}

/// <summary>
/// SCIM group member reference.
/// </summary>
public record ScimMember
{
    [JsonPropertyName("value")]
    public string Value { get; init; } = string.Empty;

    [JsonPropertyName("$ref")]
    public string? Ref { get; init; }

    [JsonPropertyName("display")]
    public string? Display { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }
}

/// <summary>
/// SCIM user creation/update request.
/// </summary>
public record ScimUserRequest
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string>? Schemas { get; init; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }

    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public ScimName? Name { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("emails")]
    public IReadOnlyList<ScimEmail>? Emails { get; init; }

    [JsonPropertyName("active")]
    public bool Active { get; init; } = true;

    [JsonPropertyName("groups")]
    public IReadOnlyList<ScimGroupReference>? Groups { get; init; }
}

/// <summary>
/// SCIM group creation/update request.
/// </summary>
public record ScimGroupRequest
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string>? Schemas { get; init; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("members")]
    public IReadOnlyList<ScimMember>? Members { get; init; }
}

/// <summary>
/// SCIM PATCH request for partial updates.
/// </summary>
public record ScimPatchRequest
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string> Schemas { get; init; } = new[] { "urn:ietf:params:scim:api:messages:2.0:PatchOp" };

    [JsonPropertyName("Operations")]
    public IReadOnlyList<ScimPatchOperation> Operations { get; init; } = Array.Empty<ScimPatchOperation>();
}

/// <summary>
/// Individual SCIM PATCH operation.
/// </summary>
public record ScimPatchOperation
{
    [JsonPropertyName("op")]
    public string Op { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("value")]
    public object? Value { get; init; }
}

/// <summary>
/// SCIM list/query request.
/// </summary>
public record ScimListRequest(
    string? Filter = null,
    int StartIndex = 1,
    int Count = 100,
    string? SortBy = null,
    string? SortOrder = null);

/// <summary>
/// SCIM list response with pagination.
/// </summary>
public record ScimListResponse<T>
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string> Schemas { get; init; } = new[] { "urn:ietf:params:scim:api:messages:2.0:ListResponse" };

    [JsonPropertyName("totalResults")]
    public int TotalResults { get; init; }

    [JsonPropertyName("startIndex")]
    public int StartIndex { get; init; }

    [JsonPropertyName("itemsPerPage")]
    public int ItemsPerPage { get; init; }

    [JsonPropertyName("Resources")]
    public IReadOnlyList<T> Resources { get; init; } = Array.Empty<T>();
}

/// <summary>
/// SCIM sync log entry for auditing.
/// </summary>
public record ScimSyncLogDto(
    ScimSyncLogId Id,
    ScimOperation Operation,
    ScimResourceType ResourceType,
    string ExternalId,
    string? InternalId,
    bool Success,
    string? ErrorMessage,
    DateTime Timestamp);

/// <summary>
/// SCIM error response (RFC 7644).
/// </summary>
public record ScimError
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string> Schemas { get; init; } = new[] { "urn:ietf:params:scim:api:messages:2.0:Error" };

    [JsonPropertyName("scimType")]
    public string? ScimType { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("status")]
    public int Status { get; init; }
}
