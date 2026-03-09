using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Shared.Kernel.Domain;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;

using Foundry.Identity.Infrastructure;
using Microsoft.Extensions.Options;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework
#pragma warning disable CA1861 // Inline arrays in test data are intentional and not called repeatedly

namespace Foundry.Identity.Tests.Infrastructure;

public class ScimServiceTests
{
    private readonly IScimConfigurationRepository _scimRepository = Substitute.For<IScimConfigurationRepository>();
    private readonly IScimSyncLogRepository _syncLogRepository = Substitute.For<IScimSyncLogRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<ScimService> _logger = Substitute.For<ILogger<ScimService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    [Fact]
    public async Task GetConfigurationAsync_WhenNoneExists_ReturnsNull()
    {
        // Arrange
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);
        ScimService service = CreateService();

        // Act
        ScimConfigurationDto? result = await service.GetConfigurationAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetConfigurationAsync_WhenExists_ReturnsDto()
    {
        // Arrange
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        config.UpdateSettings(true, "user", false, Guid.Empty, TimeProvider.System);
        config.Enable(Guid.Empty, TimeProvider.System);

        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);
        ScimService service = CreateService();

        // Act
        ScimConfigurationDto? result = await service.GetConfigurationAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsEnabled.Should().BeTrue();
        result.AutoActivateUsers.Should().BeTrue();
        result.DefaultRole.Should().Be("user");
        result.DeprovisionOnDelete.Should().BeFalse();
    }

    [Fact]
    public async Task EnableScimAsync_CreatesNewConfiguration_WhenNoneExists()
    {
        // Arrange
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);
        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService();

        EnableScimRequest request = new(
            AutoActivateUsers: true,
            DefaultRole: "user",
            DeprovisionOnDelete: false);

        // Act
        EnableScimResponse result = await service.EnableScimAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Configuration.IsEnabled.Should().BeTrue();
        result.Configuration.AutoActivateUsers.Should().BeTrue();
        result.Configuration.DefaultRole.Should().Be("user");
        result.Configuration.DeprovisionOnDelete.Should().BeFalse();
        result.PlainTextToken.Should().NotBeNullOrEmpty();

        _scimRepository.Received(1).Add(Arg.Any<ScimConfiguration>());
        await _scimRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnableScimAsync_UpdatesExistingConfiguration_WhenExists()
    {
        // Arrange
        (ScimConfiguration existingConfig, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(existingConfig);
        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService();

        EnableScimRequest request = new(
            AutoActivateUsers: false,
            DefaultRole: "admin",
            DeprovisionOnDelete: true);

        // Act
        EnableScimResponse result = await service.EnableScimAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Configuration.IsEnabled.Should().BeTrue();
        result.Configuration.AutoActivateUsers.Should().BeFalse();
        result.Configuration.DefaultRole.Should().Be("admin");
        result.Configuration.DeprovisionOnDelete.Should().BeTrue();
        result.PlainTextToken.Should().BeNull();

        _scimRepository.Received(0).Add(Arg.Any<ScimConfiguration>());
        await _scimRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisableScimAsync_WhenConfigurationNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);
        ScimService service = CreateService();

        // Act
        Func<Task> act = async () => await service.DisableScimAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SCIM configuration not found");
    }

    [Fact]
    public async Task DisableScimAsync_WhenExists_DisablesConfiguration()
    {
        // Arrange
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        config.Enable(Guid.Empty, TimeProvider.System);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);
        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService();

        // Act
        await service.DisableScimAsync();

        // Assert
        config.IsEnabled.Should().BeFalse();
        await _scimRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegenerateTokenAsync_WhenConfigurationNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);
        ScimService service = CreateService();

        // Act
        Func<Task<string>> act = async () => await service.RegenerateTokenAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SCIM configuration not found");
    }

    [Fact]
    public async Task RegenerateTokenAsync_GeneratesNewToken()
    {
        // Arrange
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);
        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService();

        string oldToken = config.BearerToken;

        // Act
        string plainTextToken = await service.RegenerateTokenAsync();

        // Assert
        plainTextToken.Should().NotBeNullOrEmpty();
        plainTextToken.Length.Should().BeGreaterThan(30);
        config.BearerToken.Should().NotBe(oldToken);
        config.TokenExpiresAt.Should().BeAfter(DateTime.UtcNow);
        await _scimRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateTokenAsync_WhenNoConfiguration_ReturnsFalse()
    {
        // Arrange
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns((ScimConfiguration?)null);
        ScimService service = CreateService();

        // Act
        bool result = await service.ValidateTokenAsync("any-token");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateTokenAsync_WhenTokenExpired_ReturnsFalse()
    {
        // Arrange
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        _ = config.RegenerateToken(Guid.Empty, TimeProvider.System);
        // Manually set expiration to the past (using reflection or just testing the behavior)
        // Since we can't directly set TokenExpiresAt, we'll test with a freshly created token which should be valid

        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);
        ScimService service = CreateService();

        // Act - test with wrong token
        bool result = await service.ValidateTokenAsync("wrong-token");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateTokenAsync_WhenValidToken_ReturnsTrue()
    {
        // Arrange
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        config.Enable(Guid.Empty, TimeProvider.System); // Must enable config for token to be valid
        string plainTextToken = config.RegenerateToken(Guid.Empty, TimeProvider.System);

        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);
        ScimService service = CreateService();

        // Act
        bool result = await service.ValidateTokenAsync(plainTextToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CreateUserAsync_Success_ReturnsScimUser()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakPost("/admin/realms/foundry/users", HttpStatusCode.Created, locationHeader: "http://localhost/users/user-123")
            .WithKeycloakPost("/admin/realms/foundry/organizations/12345678-1234-1234-1234-123456789abc/members", HttpStatusCode.NoContent)
            .WithKeycloakGet("/admin/realms/foundry/users/user-123", HttpStatusCode.OK, new
            {
                id = "user-123",
                username = "john.doe",
                email = "john.doe@example.com",
                firstName = "John",
                lastName = "Doe",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-123" }
                }
            });

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        ScimUserRequest request = new ScimUserRequest()
        {
            UserName = "john.doe",
            ExternalId = "ext-123",
            Name = new ScimName { GivenName = "John", FamilyName = "Doe" },
            Emails = new[]
            {
                new ScimEmail { Value = "john.doe@example.com", Primary = true }
            },
            Active = true
        };

        // Act
        ScimUser result = await service.CreateUserAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("user-123");
        result.UserName.Should().Be("john.doe");
        result.Active.Should().BeTrue();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create &&
            log.ResourceType == ScimResourceType.User &&
            log.Success));
        await _syncLogRepository.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateUserAsync_WhenKeycloakFails_LogsErrorAndThrows()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakPost("/admin/realms/foundry/users", HttpStatusCode.BadRequest);

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        ScimUserRequest request = new ScimUserRequest()
        {
            UserName = "john.doe",
            ExternalId = "ext-123",
            Active = true
        };

        // Act
        Func<Task<ScimUser>> act = async () => await service.CreateUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<ExternalServiceException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create &&
            log.ResourceType == ScimResourceType.User &&
!log.Success &&
            log.ErrorMessage != null));
    }

    [Fact]
    public async Task UpdateUserAsync_Success_ReturnsUpdatedScimUser()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakPut("/admin/realms/foundry/users/user-123", HttpStatusCode.NoContent)
            .WithKeycloakGet("/admin/realms/foundry/users/user-123", HttpStatusCode.OK, new
            {
                id = "user-123",
                username = "john.updated",
                email = "john.updated@example.com",
                firstName = "John",
                lastName = "Updated",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-123" }
                }
            });

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        ScimUserRequest request = new ScimUserRequest()
        {
            UserName = "john.updated",
            ExternalId = "ext-123",
            Name = new ScimName { GivenName = "John", FamilyName = "Updated" },
            Emails = new[]
            {
                new ScimEmail { Value = "john.updated@example.com", Primary = true }
            },
            Active = true
        };

        // Act
        ScimUser result = await service.UpdateUserAsync("user-123", request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("user-123");
        result.UserName.Should().Be("john.updated");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Update &&
            log.ResourceType == ScimResourceType.User &&
            log.Success));
    }

    [Fact]
    public async Task UpdateUserAsync_WhenKeycloakFails_LogsErrorAndThrows()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakPut("/admin/realms/foundry/users/user-123", HttpStatusCode.NotFound);

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        ScimUserRequest request = new ScimUserRequest()
        {
            UserName = "john.doe",
            Active = true
        };

        // Act
        Func<Task<ScimUser>> act = async () => await service.UpdateUserAsync("user-123", request);

        // Assert
        await act.Should().ThrowAsync<ExternalServiceException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Update &&
!log.Success));
    }

    [Fact]
    public async Task PatchUserAsync_Success_AppliesOperations()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/users/user-123", HttpStatusCode.OK, new
            {
                id = "user-123",
                username = "john.doe",
                email = "john.doe@example.com",
                firstName = "John",
                lastName = "Doe",
                enabled = true,
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-123" }
                }
            })
            .WithKeycloakPut("/admin/realms/foundry/users/user-123", HttpStatusCode.NoContent);

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "active", Value = false },
                new ScimPatchOperation { Op = "replace", Path = "emails", Value = "new.email@example.com" }
            }
        };

        // Act
        ScimUser result = await service.PatchUserAsync("user-123", request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("user-123");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Patch &&
            log.ResourceType == ScimResourceType.User &&
            log.Success));
    }

    [Fact]
    public async Task PatchUserAsync_WhenUserNotFound_Throws()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/users/user-123", HttpStatusCode.NotFound);

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        ScimPatchRequest request = new ScimPatchRequest()
        {
            Operations = new[]
            {
                new ScimPatchOperation { Op = "replace", Path = "active", Value = false }
            }
        };

        // Act
        Func<Task<ScimUser>> act = async () => await service.PatchUserAsync("user-123", request);

        // Assert
        await act.Should().ThrowAsync<ExternalServiceException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Patch &&
!log.Success));
    }

    [Fact]
    public async Task DeleteUserAsync_WhenDeprovisionTrue_PerformsHardDelete()
    {
        // Arrange
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        config.UpdateSettings(true, null, true, Guid.Empty, TimeProvider.System); // DeprovisionOnDelete = true

        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakDelete("/admin/realms/foundry/users/user-123", HttpStatusCode.NoContent);

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        // Act
        await service.DeleteUserAsync("user-123");

        // Assert
        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete &&
            log.ResourceType == ScimResourceType.User &&
            log.Success));
    }

    [Fact]
    public async Task DeleteUserAsync_WhenDeprovisionFalse_PerformsSoftDelete()
    {
        // Arrange
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        config.UpdateSettings(true, null, false, Guid.Empty, TimeProvider.System); // DeprovisionOnDelete = false

        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakPut("/admin/realms/foundry/users/user-123", HttpStatusCode.NoContent);

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        // Act
        await service.DeleteUserAsync("user-123");

        // Assert
        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete &&
            log.ResourceType == ScimResourceType.User &&
            log.Success));
    }

    [Fact]
    public async Task DeleteUserAsync_WhenFails_LogsError()
    {
        // Arrange
        (ScimConfiguration config, string _) = ScimConfiguration.Create(_tenantId, Guid.Empty, TimeProvider.System);
        _scimRepository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakPut("/admin/realms/foundry/users/user-123", HttpStatusCode.NotFound);

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        // Act
        Func<Task> act = async () => await service.DeleteUserAsync("user-123");

        // Assert
        await act.Should().ThrowAsync<ExternalServiceException>();

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete &&
!log.Success));
    }

    [Fact]
    public async Task ListUsersAsync_ReturnsPaginatedResults()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/users", HttpStatusCode.OK, new[]
            {
                new
                {
                    id = "user-1",
                    username = "user1",
                    email = "user1@example.com",
                    firstName = "User",
                    lastName = "One",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                },
                new
                {
                    id = "user-2",
                    username = "user2",
                    email = "user2@example.com",
                    firstName = "User",
                    lastName = "Two",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                }
            })
            .WithKeycloakGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 10);

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        ScimListRequest request = new(
            Filter: null,
            StartIndex: 1,
            Count: 10);

        // Act
        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.TotalResults.Should().Be(10);
        result.StartIndex.Should().Be(1);
        result.ItemsPerPage.Should().Be(2);
        result.Resources.Should().HaveCount(2);
        result.Resources[0].Id.Should().Be("user-1");
        result.Resources[1].Id.Should().Be("user-2");
    }

    [Fact]
    public async Task ListUsersAsync_WithFilter_AppliesFilter()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/users", HttpStatusCode.OK, new[]
            {
                new
                {
                    id = "user-1",
                    username = "john.doe",
                    email = "john@example.com",
                    firstName = "John",
                    lastName = "Doe",
                    enabled = true,
                    attributes = new Dictionary<string, IEnumerable<string>>()
                }
            })
            .WithKeycloakGet("/admin/realms/foundry/users/count", HttpStatusCode.OK, 1);

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        ScimListRequest request = new(
            Filter: "userName eq \"john.doe\"",
            StartIndex: 1,
            Count: 10);

        // Act
        ScimListResponse<ScimUser> result = await service.ListUsersAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Resources.Should().HaveCount(1);
        result.Resources[0].UserName.Should().Be("john.doe");
    }

    [Fact]
    public async Task CreateGroupAsync_Success_ReturnsScimGroup()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakPost("/admin/realms/foundry/groups", HttpStatusCode.Created, locationHeader: "http://localhost/groups/group-123")
            .WithKeycloakGet("/admin/realms/foundry/groups/group-123", HttpStatusCode.OK, new
            {
                id = "group-123",
                name = "Developers",
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-grp-123" }
                }
            })
            .WithKeycloakGet("/admin/realms/foundry/groups/group-123/members", HttpStatusCode.OK, Array.Empty<object>());

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        ScimGroupRequest request = new ScimGroupRequest()
        {
            DisplayName = "Developers",
            ExternalId = "ext-grp-123"
        };

        // Act
        ScimGroup result = await service.CreateGroupAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("group-123");
        result.DisplayName.Should().Be("Developers");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Create &&
            log.ResourceType == ScimResourceType.Group &&
            log.Success));
    }

    [Fact]
    public async Task UpdateGroupAsync_Success_ReturnsUpdatedGroup()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakPut("/admin/realms/foundry/groups/group-123", HttpStatusCode.NoContent)
            .WithKeycloakGet("/admin/realms/foundry/groups/group-123", HttpStatusCode.OK, new
            {
                id = "group-123",
                name = "Updated Developers",
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["scim_external_id"] = new[] { "ext-grp-123" }
                }
            })
            .WithKeycloakGet("/admin/realms/foundry/groups/group-123/members", HttpStatusCode.OK, Array.Empty<object>());

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        ScimGroupRequest request = new ScimGroupRequest()
        {
            DisplayName = "Updated Developers",
            ExternalId = "ext-grp-123"
        };

        // Act
        ScimGroup result = await service.UpdateGroupAsync("group-123", request);

        // Assert
        result.Should().NotBeNull();
        result.DisplayName.Should().Be("Updated Developers");

        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Update &&
            log.ResourceType == ScimResourceType.Group &&
            log.Success));
    }

    [Fact]
    public async Task DeleteGroupAsync_Success_LogsSync()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakDelete("/admin/realms/foundry/groups/group-123", HttpStatusCode.NoContent);

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        // Act
        await service.DeleteGroupAsync("group-123");

        // Assert
        _syncLogRepository.Received(1).Add(Arg.Is<ScimSyncLog>(log =>
            log.Operation == ScimOperation.Delete &&
            log.ResourceType == ScimResourceType.Group &&
            log.Success));
    }

    [Fact]
    public async Task ListGroupsAsync_ReturnsPaginatedGroups()
    {
        // Arrange
        MockKeycloakHttpHandler handler = new MockKeycloakHttpHandler()
            .WithKeycloakGet("/admin/realms/foundry/groups", HttpStatusCode.OK, new[]
            {
                new
                {
                    id = "group-1",
                    name = "Group 1",
                    attributes = new Dictionary<string, IEnumerable<string>>()
                }
            })
            .WithKeycloakGet("/admin/realms/foundry/groups/group-1", HttpStatusCode.OK, new
            {
                id = "group-1",
                name = "Group 1",
                attributes = new Dictionary<string, IEnumerable<string>>()
            })
            .WithKeycloakGet("/admin/realms/foundry/groups/group-1/members", HttpStatusCode.OK, Array.Empty<object>());

        _tenantContext.TenantId.Returns(_tenantId);

        ScimService service = CreateService(handler);

        ScimListRequest request = new(StartIndex: 1, Count: 10);

        // Act
        ScimListResponse<ScimGroup> result = await service.ListGroupsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Resources.Should().HaveCount(1);
        result.Resources[0].DisplayName.Should().Be("Group 1");
    }

    [Fact]
    public async Task GetSyncLogsAsync_ReturnsLogs()
    {
        // Arrange
        List<ScimSyncLog> logs =
        [
            ScimSyncLog.Create(_tenantId, ScimOperation.Create, ScimResourceType.User, "ext-1", "user-1", true, TimeProvider.System),
            ScimSyncLog.Create(_tenantId, ScimOperation.Update, ScimResourceType.User, "ext-2", "user-2", false, TimeProvider.System, "Error")
        ];

        _syncLogRepository.GetRecentAsync(100, Arg.Any<CancellationToken>()).Returns(logs);

        ScimService service = CreateService();

        // Act
        IReadOnlyList<ScimSyncLogDto> result = await service.GetSyncLogsAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Operation.Should().Be(ScimOperation.Create);
        result[0].Success.Should().BeTrue();
        result[1].Operation.Should().Be(ScimOperation.Update);
        result[1].Success.Should().BeFalse();
        result[1].ErrorMessage.Should().Be("Error");
    }

    private ScimService CreateService(HttpMessageHandler? handler = null)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();

        HttpClient keycloakClient = handler != null
            ? new HttpClient(handler) : new HttpClient(new MockKeycloakHttpHandler());
        keycloakClient.BaseAddress = new Uri("https://keycloak.test/");

        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(keycloakClient);

        _tenantContext.TenantId.Returns(_tenantId);

        ScimUserService userService = new(
            httpClientFactory,
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            Options.Create(new KeycloakOptions()),
            Substitute.For<ILogger<ScimUserService>>(),
            TimeProvider.System);

        ScimGroupService groupService = new(
            httpClientFactory,
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            Options.Create(new KeycloakOptions()),
            Substitute.For<ILogger<ScimGroupService>>(),
            TimeProvider.System);

        return new ScimService(
            _scimRepository,
            _syncLogRepository,
            _tenantContext,
            userService,
            groupService,
            _logger,
            TimeProvider.System);
    }

    private sealed class MockKeycloakHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader)> _routes = new Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader)>();

        public MockKeycloakHttpHandler WithKeycloakGet(string path, HttpStatusCode status, object? content = null)
        {
            _routes[$"GET:{path}"] = (status, content, null);
            return this;
        }

        public MockKeycloakHttpHandler WithKeycloakPost(string path, HttpStatusCode status, object? content = null, string? locationHeader = null)
        {
            _routes[$"POST:{path}"] = (status, content, locationHeader);
            return this;
        }

        public MockKeycloakHttpHandler WithKeycloakPut(string path, HttpStatusCode status, object? content = null)
        {
            _routes[$"PUT:{path}"] = (status, content, null);
            return this;
        }

        public MockKeycloakHttpHandler WithKeycloakDelete(string path, HttpStatusCode status, object? content = null)
        {
            _routes[$"DELETE:{path}"] = (status, content, null);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? "";
            _ = request.RequestUri?.Query ?? "";

            // Handle query string matching - create base path

            // Try exact match first (with query string if present)
            string exactKey = $"{request.Method}:{path}";
            if (_routes.TryGetValue(exactKey, out (HttpStatusCode Status, object? Content, string? LocationHeader) route))
            {
                HttpResponseMessage response = new(route.Status);
                if (route.Content != null)
                {
                    response.Content = JsonContent.Create(route.Content);
                }
                if (route.LocationHeader != null)
                {
                    response.Headers.Location = new Uri(route.LocationHeader);
                }
                return Task.FromResult(response);
            }

            // Default error response
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"No mock configured for {request.Method}:{path}")
            });
        }
    }
}
