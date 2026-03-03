using System.Security.Cryptography;
using System.Text;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Foundry.Identity.Infrastructure.Services;

public sealed partial class ScimService : IScimService
{
    private readonly IScimConfigurationRepository _scimRepository;
    private readonly IScimSyncLogRepository _syncLogRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ScimUserService _userService;
    private readonly ScimGroupService _groupService;
    private readonly ILogger<ScimService> _logger;

    public ScimService(
        IScimConfigurationRepository scimRepository,
        IScimSyncLogRepository syncLogRepository,
        ITenantContext tenantContext,
        ScimUserService userService,
        ScimGroupService groupService,
        ILogger<ScimService> logger)
    {
        _scimRepository = scimRepository;
        _syncLogRepository = syncLogRepository;
        _tenantContext = tenantContext;
        _userService = userService;
        _groupService = groupService;
        _logger = logger;
    }

    public async Task<ScimConfigurationDto?> GetConfigurationAsync(CancellationToken ct = default)
    {
        ScimConfiguration? config = await _scimRepository.GetAsync(ct);
        if (config == null)
        {
            return null;
        }

        return MapToDto(config);
    }

    public async Task<EnableScimResponse> EnableScimAsync(EnableScimRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = _tenantContext.TenantId;

        LogEnablingScim(tenantId.Value);

        string? plainTextToken = null;
        ScimConfiguration? config = await _scimRepository.GetAsync(ct);
        if (config == null)
        {
            (ScimConfiguration newConfig, string token) = ScimConfiguration.Create(tenantId, Guid.Empty);
            config = newConfig;
            plainTextToken = token;
            _scimRepository.Add(config);
        }

        config.UpdateSettings(
            request.AutoActivateUsers,
            request.DefaultRole,
            request.DeprovisionOnDelete,
            Guid.Empty);

        config.Enable(Guid.Empty);

        await _scimRepository.SaveChangesAsync(ct);

        LogScimEnabled(tenantId.Value);

        return new EnableScimResponse(MapToDto(config), plainTextToken);
    }

    public async Task DisableScimAsync(CancellationToken ct = default)
    {
        ScimConfiguration? config = await _scimRepository.GetAsync(ct);
        if (config == null)
        {
            throw new InvalidOperationException("SCIM configuration not found");
        }

        LogDisablingScim(_tenantContext.TenantId.Value);

        config.Disable(Guid.Empty);
        await _scimRepository.SaveChangesAsync(ct);

        LogScimDisabled(_tenantContext.TenantId.Value);
    }

    public async Task<string> RegenerateTokenAsync(CancellationToken ct = default)
    {
        ScimConfiguration? config = await _scimRepository.GetAsync(ct);
        if (config == null)
        {
            throw new InvalidOperationException("SCIM configuration not found");
        }

        LogRegeneratingToken(_tenantContext.TenantId.Value);

        string plainTextToken = config.RegenerateToken(Guid.Empty);
        await _scimRepository.SaveChangesAsync(ct);

        return plainTextToken;
    }

    public Task<ScimUser> CreateUserAsync(ScimUserRequest request, CancellationToken ct = default)
        => _userService.CreateUserAsync(request, ct);

    public Task<ScimUser> UpdateUserAsync(string id, ScimUserRequest request, CancellationToken ct = default)
        => _userService.UpdateUserAsync(id, request, ct);

    public Task<ScimUser> PatchUserAsync(string id, ScimPatchRequest request, CancellationToken ct = default)
        => _userService.PatchUserAsync(id, request, ct);

    public Task DeleteUserAsync(string id, CancellationToken ct = default)
        => _userService.DeleteUserAsync(id, ct);

    public Task<ScimUser?> GetUserAsync(string id, CancellationToken ct = default)
        => _userService.GetUserAsync(id, ct);

    public Task<ScimListResponse<ScimUser>> ListUsersAsync(ScimListRequest request, CancellationToken ct = default)
        => _userService.ListUsersAsync(request, ct);

    public Task<ScimGroup> CreateGroupAsync(ScimGroupRequest request, CancellationToken ct = default)
        => _groupService.CreateGroupAsync(request, ct);

    public Task<ScimGroup> UpdateGroupAsync(string id, ScimGroupRequest request, CancellationToken ct = default)
        => _groupService.UpdateGroupAsync(id, request, ct);

    public Task DeleteGroupAsync(string id, CancellationToken ct = default)
        => _groupService.DeleteGroupAsync(id, ct);

    public Task<ScimListResponse<ScimGroup>> ListGroupsAsync(ScimListRequest request, CancellationToken ct = default)
        => _groupService.ListGroupsAsync(request, ct);

    public async Task<IReadOnlyList<ScimSyncLogDto>> GetSyncLogsAsync(int limit = 100, CancellationToken ct = default)
    {
        IReadOnlyList<ScimSyncLog> logs = await _syncLogRepository.GetRecentAsync(limit, ct);
        return logs.Select(l => new ScimSyncLogDto(
            l.Id,
            l.Operation,
            l.ResourceType,
            l.ExternalId,
            l.InternalId,
            l.Success,
            l.ErrorMessage,
            l.Timestamp)).ToList();
    }

    public async Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        ScimConfiguration? config = await _scimRepository.GetAsync(ct);
        if (config == null || !config.IsTokenValid())
        {
            return false;
        }

        string hashedToken = HashToken(token);
        byte[] expectedBytes = Encoding.UTF8.GetBytes(config.BearerToken);
        byte[] actualBytes = Encoding.UTF8.GetBytes(hashedToken);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private ScimConfigurationDto MapToDto(ScimConfiguration config)
    {
        return new ScimConfigurationDto(
            config.IsEnabled,
            config.TokenPrefix,
            config.TokenExpiresAt,
            config.LastSyncAt,
            GetScimEndpointUrl(),
            config.AutoActivateUsers,
            config.DefaultRole,
            config.DeprovisionOnDelete);
    }

    private string GetScimEndpointUrl()
    {
        return "/scim/v2";
    }

    private static string HashToken(string token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(token);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}

internal sealed record ScimKeycloakUserRepresentation
{
    public string? Id { get; init; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool? Enabled { get; set; }
    public Dictionary<string, IEnumerable<string>>? Attributes { get; init; }
}

internal sealed record ScimKeycloakGroupRepresentation
{
    public string? Id { get; init; }
    public string? Name { get; init; }
    public Dictionary<string, IEnumerable<string>>? Attributes { get; init; }
}

internal sealed record ScimKeycloakRoleRepresentation
{
    public string? Id { get; init; }
    public string? Name { get; init; }
}

public sealed partial class ScimService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Enabling SCIM for tenant {TenantId}")]
    private partial void LogEnablingScim(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM enabled for tenant {TenantId}")]
    private partial void LogScimEnabled(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Disabling SCIM for tenant {TenantId}")]
    private partial void LogDisablingScim(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SCIM disabled for tenant {TenantId}")]
    private partial void LogScimDisabled(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Regenerating SCIM token for tenant {TenantId}")]
    private partial void LogRegeneratingToken(Guid tenantId);
}
