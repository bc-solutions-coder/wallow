using System.Text.Json.Serialization;
using Asp.Versioning;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.Api.Controllers;

/// <summary>
/// SCIM 2.0 API endpoints for enterprise identity provider integration.
/// Follows RFC 7644 (SCIM Protocol) specification.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("scim/v2")]
[AllowAnonymous] // SCIM uses Bearer token authentication via middleware (not OAuth)
[Tags("SCIM")]
[Produces("application/scim+json", "application/json")]
[Consumes("application/scim+json", "application/json")]
public partial class ScimController : ControllerBase
{
    private static readonly string[] _resourceTypeSchema = ["urn:ietf:params:scim:schemas:core:2.0:ResourceType"];

    private readonly IScimService _scimService;
    private readonly ILogger<ScimController> _logger;
    private readonly IHostEnvironment _environment;

    public ScimController(
        IScimService scimService,
        ILogger<ScimController> logger,
        IHostEnvironment environment)
    {
        _scimService = scimService;
        _logger = logger;
        _environment = environment;
    }

    #region Users

    /// <summary>
    /// List users with optional filtering and pagination.
    /// </summary>
    [HttpGet("Users")]
    [ProducesResponseType(typeof(ScimListResponse<ScimUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ScimListResponse<ScimUser>>> ListUsers(
        [FromQuery] string? filter,
        [FromQuery] int startIndex = 1,
        [FromQuery] int count = 100,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        CancellationToken ct = default)
    {
        ScimListRequest request = new(filter, startIndex, count, sortBy, sortOrder);
        ScimListResponse<ScimUser> result = await _scimService.ListUsersAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a user by ID.
    /// </summary>
    [HttpGet("Users/{id}")]
    [ProducesResponseType(typeof(ScimUser), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScimUser>> GetScimUser(string id, CancellationToken ct)
    {
        ScimUser? user = await _scimService.GetUserAsync(id, ct);
        if (user is null)
        {
            return NotFound(new ScimError
            {
                Status = 404,
                Detail = $"User with id '{id}' not found"
            });
        }
        return Ok(user);
    }

    /// <summary>
    /// Create a new user (SCIM provisioning).
    /// </summary>
    [HttpPost("Users")]
    [ProducesResponseType(typeof(ScimUser), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ScimUser>> CreateUser(
        [FromBody] ScimUserRequest request,
        CancellationToken ct)
    {
        try
        {
            ScimUser user = await _scimService.CreateUserAsync(request, ct);
            return Created($"/scim/v2/Users/{user.Id}", user);
        }
        catch (Exception ex)
        {
            LogScimOperationError(ex, "CreateUser");
            return BadRequest(new ScimError
            {
                Status = 400,
                ScimType = "invalidValue",
                Detail = _environment.IsDevelopment() ? ex.Message : "An unexpected error occurred"
            });
        }
    }

    /// <summary>
    /// Replace a user (full update).
    /// </summary>
    [HttpPut("Users/{id}")]
    [ProducesResponseType(typeof(ScimUser), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScimUser>> UpdateUser(
        string id,
        [FromBody] ScimUserRequest request,
        CancellationToken ct)
    {
        try
        {
            ScimUser user = await _scimService.UpdateUserAsync(id, request, ct);
            return Ok(user);
        }
        catch (Exception ex)
        {
            LogScimOperationError(ex, "UpdateUser");
            return BadRequest(new ScimError
            {
                Status = 400,
                Detail = _environment.IsDevelopment() ? ex.Message : "An unexpected error occurred"
            });
        }
    }

    /// <summary>
    /// Partially update a user (SCIM PATCH).
    /// </summary>
    [HttpPatch("Users/{id}")]
    [ProducesResponseType(typeof(ScimUser), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScimUser>> PatchUser(
        string id,
        [FromBody] ScimPatchRequest request,
        CancellationToken ct)
    {
        try
        {
            ScimUser user = await _scimService.PatchUserAsync(id, request, ct);
            return Ok(user);
        }
        catch (Exception ex)
        {
            LogScimOperationError(ex, "PatchUser");
            return BadRequest(new ScimError
            {
                Status = 400,
                Detail = _environment.IsDevelopment() ? ex.Message : "An unexpected error occurred"
            });
        }
    }

    /// <summary>
    /// Delete a user (deprovisioning).
    /// </summary>
    [HttpDelete("Users/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(string id, CancellationToken ct)
    {
        try
        {
            await _scimService.DeleteUserAsync(id, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            LogScimOperationError(ex, "DeleteUser");
            return BadRequest(new ScimError
            {
                Status = 400,
                Detail = _environment.IsDevelopment() ? ex.Message : "An unexpected error occurred"
            });
        }
    }

    #endregion

    #region Groups

    /// <summary>
    /// List groups with optional filtering and pagination.
    /// </summary>
    [HttpGet("Groups")]
    [ProducesResponseType(typeof(ScimListResponse<ScimGroup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScimListResponse<ScimGroup>>> ListGroups(
        [FromQuery] string? filter,
        [FromQuery] int startIndex = 1,
        [FromQuery] int count = 100,
        CancellationToken ct = default)
    {
        ScimListRequest request = new(filter, startIndex, count);
        ScimListResponse<ScimGroup> result = await _scimService.ListGroupsAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a group by ID.
    /// </summary>
    [HttpGet("Groups/{id}")]
    [ProducesResponseType(typeof(ScimGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScimGroup>> GetGroup(string id, CancellationToken ct)
    {
        ScimGroup? group = await _scimService.GetGroupAsync(id, ct);
        if (group is null)
        {
            return NotFound(new ScimError
            {
                Status = 404,
                Detail = $"Group with id '{id}' not found"
            });
        }
        return Ok(group);
    }

    /// <summary>
    /// Create a new group.
    /// </summary>
    [HttpPost("Groups")]
    [ProducesResponseType(typeof(ScimGroup), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ScimGroup>> CreateGroup(
        [FromBody] ScimGroupRequest request,
        CancellationToken ct)
    {
        try
        {
            ScimGroup group = await _scimService.CreateGroupAsync(request, ct);
            return Created($"/scim/v2/Groups/{group.Id}", group);
        }
        catch (Exception ex)
        {
            LogScimOperationError(ex, "CreateGroup");
            return BadRequest(new ScimError
            {
                Status = 400,
                Detail = _environment.IsDevelopment() ? ex.Message : "An unexpected error occurred"
            });
        }
    }

    /// <summary>
    /// Replace a group (full update).
    /// </summary>
    [HttpPut("Groups/{id}")]
    [ProducesResponseType(typeof(ScimGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScimGroup>> UpdateGroup(
        string id,
        [FromBody] ScimGroupRequest request,
        CancellationToken ct)
    {
        try
        {
            ScimGroup group = await _scimService.UpdateGroupAsync(id, request, ct);
            return Ok(group);
        }
        catch (Exception ex)
        {
            LogScimOperationError(ex, "UpdateGroup");
            return BadRequest(new ScimError
            {
                Status = 400,
                Detail = _environment.IsDevelopment() ? ex.Message : "An unexpected error occurred"
            });
        }
    }

    /// <summary>
    /// Delete a group.
    /// </summary>
    [HttpDelete("Groups/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ScimError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGroup(string id, CancellationToken ct)
    {
        try
        {
            await _scimService.DeleteGroupAsync(id, ct);
            return NoContent();
        }
        catch (Exception ex)
        {
            LogScimOperationError(ex, "DeleteGroup");
            return BadRequest(new ScimError
            {
                Status = 400,
                Detail = _environment.IsDevelopment() ? ex.Message : "An unexpected error occurred"
            });
        }
    }

    #endregion

    #region Discovery

    /// <summary>
    /// Get SCIM Service Provider configuration.
    /// Describes the SCIM capabilities of this server.
    /// </summary>
    [HttpGet("ServiceProviderConfig")]
    [ProducesResponseType(typeof(ScimServiceProviderConfig), StatusCodes.Status200OK)]
    public ActionResult<ScimServiceProviderConfig> GetServiceProviderConfig()
    {
        ScimServiceProviderConfig config = new()
        {
            Schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig" },
            DocumentationUri = "https://docs.foundry.dev/scim",
            Patch = new ScimConfigFeature { Supported = true },
            Bulk = new ScimBulkConfig { Supported = false, MaxOperations = 0, MaxPayloadSize = 0 },
            Filter = new ScimFilterConfig { Supported = true, MaxResults = 200 },
            ChangePassword = new ScimConfigFeature { Supported = false },
            Sort = new ScimConfigFeature { Supported = true },
            Etag = new ScimConfigFeature { Supported = false },
            AuthenticationSchemes = new[]
            {
                new ScimAuthenticationScheme
                {
                    Type = "oauthbearertoken",
                    Name = "OAuth Bearer Token",
                    Description = "Authentication using OAuth 2.0 Bearer Token",
                    Primary = true
                }
            }
        };

        return Ok(config);
    }

    /// <summary>
    /// Get SCIM schemas.
    /// Describes the structure of User and Group resources.
    /// </summary>
    [HttpGet("Schemas")]
    [ProducesResponseType(typeof(ScimListResponse<ScimSchema>), StatusCodes.Status200OK)]
    public ActionResult<ScimListResponse<ScimSchema>> GetSchemas()
    {
        List<ScimSchema> schemas =
        [
            new()
            {
                Id = "urn:ietf:params:scim:schemas:core:2.0:User",
                Name = "User",
                Description = "User Account"
            },
            new()
            {
                Id = "urn:ietf:params:scim:schemas:core:2.0:Group",
                Name = "Group",
                Description = "Group"
            }
        ];

        return Ok(new ScimListResponse<ScimSchema>
        {
            TotalResults = schemas.Count,
            StartIndex = 1,
            ItemsPerPage = schemas.Count,
            Resources = schemas
        });
    }

    /// <summary>
    /// Get SCIM resource types.
    /// Describes the available resource types (User, Group).
    /// </summary>
    [HttpGet("ResourceTypes")]
    [ProducesResponseType(typeof(ScimListResponse<ScimResourceType>), StatusCodes.Status200OK)]
    public ActionResult<ScimListResponse<ScimResourceType>> GetResourceTypes()
    {
        List<ScimResourceType> resourceTypes =
        [
            new()
            {
                Schemas = _resourceTypeSchema,
                Id = "User",
                Name = "User",
                Description = "User Account",
                Endpoint = "/Users",
                Schema = "urn:ietf:params:scim:schemas:core:2.0:User"
            },
            new()
            {
                Schemas = _resourceTypeSchema,
                Id = "Group",
                Name = "Group",
                Description = "Group",
                Endpoint = "/Groups",
                Schema = "urn:ietf:params:scim:schemas:core:2.0:Group"
            }
        ];

        return Ok(new ScimListResponse<ScimResourceType>
        {
            TotalResults = resourceTypes.Count,
            StartIndex = 1,
            ItemsPerPage = resourceTypes.Count,
            Resources = resourceTypes
        });
    }

    #endregion
}

public partial class ScimController
{
    [LoggerMessage(Level = LogLevel.Error, Message = "SCIM operation {Operation} failed")]
    private partial void LogScimOperationError(Exception ex, string operation);
}

#region SCIM Discovery DTOs

/// <summary>
/// SCIM Service Provider configuration.
/// </summary>
public record ScimServiceProviderConfig
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string> Schemas { get; init; } = Array.Empty<string>();

    [JsonPropertyName("documentationUri")]
    public string? DocumentationUri { get; init; }

    [JsonPropertyName("patch")]
    public ScimConfigFeature? Patch { get; init; }

    [JsonPropertyName("bulk")]
    public ScimBulkConfig? Bulk { get; init; }

    [JsonPropertyName("filter")]
    public ScimFilterConfig? Filter { get; init; }

    [JsonPropertyName("changePassword")]
    public ScimConfigFeature? ChangePassword { get; init; }

    [JsonPropertyName("sort")]
    public ScimConfigFeature? Sort { get; init; }

    [JsonPropertyName("etag")]
    public ScimConfigFeature? Etag { get; init; }

    [JsonPropertyName("authenticationSchemes")]
    public IReadOnlyList<ScimAuthenticationScheme>? AuthenticationSchemes { get; init; }
}

/// <summary>
/// SCIM feature support indicator.
/// </summary>
public record ScimConfigFeature
{
    [JsonPropertyName("supported")]
    public bool Supported { get; init; }
}

/// <summary>
/// SCIM bulk operation configuration.
/// </summary>
public record ScimBulkConfig
{
    [JsonPropertyName("supported")]
    public bool Supported { get; init; }

    [JsonPropertyName("maxOperations")]
    public int MaxOperations { get; init; }

    [JsonPropertyName("maxPayloadSize")]
    public int MaxPayloadSize { get; init; }
}

/// <summary>
/// SCIM filter configuration.
/// </summary>
public record ScimFilterConfig
{
    [JsonPropertyName("supported")]
    public bool Supported { get; init; }

    [JsonPropertyName("maxResults")]
    public int MaxResults { get; init; }
}

/// <summary>
/// SCIM authentication scheme.
/// </summary>
public record ScimAuthenticationScheme
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("primary")]
    public bool Primary { get; init; }
}

/// <summary>
/// SCIM schema definition.
/// </summary>
public record ScimSchema
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string> Schemas { get; init; } = new[] { "urn:ietf:params:scim:schemas:core:2.0:Schema" };

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// SCIM resource type definition.
/// </summary>
public record ScimResourceType
{
    [JsonPropertyName("schemas")]
    public IReadOnlyList<string> Schemas { get; init; } = Array.Empty<string>();

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; init; } = string.Empty;

    [JsonPropertyName("schema")]
    public string Schema { get; init; } = string.Empty;
}

#endregion
