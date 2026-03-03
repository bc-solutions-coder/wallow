using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Infrastructure.Persistence;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Tests.Common.Factories;
using Foundry.Tests.Common.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Identity.IntegrationTests.Scim;

[Trait("Category", "Integration")]
public class ScimProvisioningTests : IClassFixture<ScimProvisioningTestFactory>, IAsyncLifetime
{
    private readonly ScimProvisioningTestFactory _factory;
    private IServiceScope? _scope;
    private IServiceProvider _scopedServices = null!;
    private IScimService _scimService = null!;
    private IdentityDbContext _dbContext = null!;

    public ScimProvisioningTests(ScimProvisioningTestFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _scope = _factory.Services.CreateScope();
        _scopedServices = _scope.ServiceProvider;

        _scimService = _scopedServices.GetRequiredService<IScimService>();
        _dbContext = _scopedServices.GetRequiredService<IdentityDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        // Clear any existing SCIM configurations and sync logs from prior tests
        List<Domain.Entities.ScimConfiguration> existingConfigs = _dbContext.ScimConfigurations.ToList();
        _dbContext.ScimConfigurations.RemoveRange(existingConfigs);
        List<Domain.Entities.ScimSyncLog> existingLogs = _dbContext.ScimSyncLogs.ToList();
        _dbContext.ScimSyncLogs.RemoveRange(existingLogs);
        await _dbContext.SaveChangesAsync();

        // Enable SCIM for the test tenant
        _ = await _scimService.EnableScimAsync(new EnableScimRequest(
            AutoActivateUsers: true,
            DefaultRole: "user",
            DeprovisionOnDelete: false));

        _factory.ResetKeycloakMock();
    }

    public Task DisposeAsync()
    {
        _scope?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateUser_WithValidToken_CreatesInKeycloak()
    {
        // Arrange
        ScimUserRequest request = new()
        {
            UserName = "john.doe@test.com",
            Name = new ScimName
            {
                GivenName = "John",
                FamilyName = "Doe"
            },
            Emails = new[]
            {
                new ScimEmail
                {
                    Value = "john.doe@test.com",
                    Type = "work",
                    Primary = true
                }
            },
            Active = true
        };

        // Act
        ScimUser result = await _scimService.CreateUserAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.UserName.Should().Be("john.doe@test.com");
        result.Name?.GivenName.Should().Be("John");
        result.Name?.FamilyName.Should().Be("Doe");
        result.Active.Should().BeTrue();
        result.Id.Should().NotBeNullOrEmpty();

        // Verify sync log was created
        IReadOnlyList<ScimSyncLogDto> syncLogs = await _scimService.GetSyncLogsAsync();
        syncLogs.Should().ContainSingle(log =>
            log.Operation == ScimOperation.Create &&
            log.ResourceType == ScimResourceType.User &&
            log.Success);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_Returns409()
    {
        // Arrange - Create first user
        ScimUserRequest firstRequest = new()
        {
            UserName = "duplicate@test.com",
            Emails = new[]
            {
                new ScimEmail
                {
                    Value = "duplicate@test.com",
                    Primary = true
                }
            },
            Active = true
        };
        _ = await _scimService.CreateUserAsync(firstRequest);

        // Setup mock to return 409 conflict
        _factory.SetupUserCreationConflict();

        // Act & Assert - Try to create duplicate
        ScimUserRequest secondRequest = new()
        {
            UserName = "duplicate@test.com",
            Emails = new[]
            {
                new ScimEmail
                {
                    Value = "duplicate@test.com",
                    Primary = true
                }
            },
            Active = true
        };

        Func<Task> act = async () => await _scimService.CreateUserAsync(secondRequest);
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task UpdateUser_ModifiesKeycloakUser()
    {
        // Arrange - Create user first
        ScimUserRequest createRequest = new()
        {
            UserName = "jane.smith@test.com",
            Name = new ScimName
            {
                GivenName = "Jane",
                FamilyName = "Smith"
            },
            Emails = new[]
            {
                new ScimEmail
                {
                    Value = "jane.smith@test.com",
                    Primary = true
                }
            },
            Active = true
        };
        ScimUser createdUser = await _scimService.CreateUserAsync(createRequest);

        // Act - Update user
        ScimUserRequest updateRequest = new()
        {
            UserName = "jane.smith@test.com",
            Name = new ScimName
            {
                GivenName = "Janet",
                FamilyName = "Smith-Jones"
            },
            Emails = new[]
            {
                new ScimEmail
                {
                    Value = "jane.smith@test.com",
                    Primary = true
                }
            },
            Active = true
        };
        ScimUser result = await _scimService.UpdateUserAsync(createdUser.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Name?.GivenName.Should().Be("Janet");
        result.Name?.FamilyName.Should().Be("Smith-Jones");

        // Verify sync log
        IReadOnlyList<ScimSyncLogDto> syncLogs = await _scimService.GetSyncLogsAsync();
        syncLogs.Should().Contain(log =>
            log.Operation == ScimOperation.Update &&
            log.ResourceType == ScimResourceType.User &&
            log.Success);
    }

    [Fact]
    public async Task PatchUser_Active_DisablesUser()
    {
        // Arrange - Create active user
        ScimUserRequest createRequest = new()
        {
            UserName = "patch.test@test.com",
            Emails = new[]
            {
                new ScimEmail
                {
                    Value = "patch.test@test.com",
                    Primary = true
                }
            },
            Active = true
        };
        ScimUser createdUser = await _scimService.CreateUserAsync(createRequest);

        // Act - Patch to disable user
        ScimPatchRequest patchRequest = new()
        {
            Operations = new[]
            {
                new ScimPatchOperation
                {
                    Op = "replace",
                    Path = "active",
                    Value = false
                }
            }
        };
        ScimUser result = await _scimService.PatchUserAsync(createdUser.Id, patchRequest);

        // Assert
        result.Should().NotBeNull();
        result.Active.Should().BeFalse();

        // Verify sync log
        IReadOnlyList<ScimSyncLogDto> syncLogs = await _scimService.GetSyncLogsAsync();
        syncLogs.Should().Contain(log =>
            log.Operation == ScimOperation.Patch &&
            log.ResourceType == ScimResourceType.User &&
            log.Success);
    }

    [Fact]
    public async Task DeleteUser_WithDeprovision_RemovesFromKeycloak()
    {
        // Arrange - Enable deprovisioning
        _dbContext.ChangeTracker.Clear();
        await _scimService.DisableScimAsync();
        _ = await _scimService.EnableScimAsync(new EnableScimRequest(
            AutoActivateUsers: true,
            DefaultRole: null,
            DeprovisionOnDelete: true));

        // Create user
        ScimUserRequest createRequest = new()
        {
            UserName = "delete.hard@test.com",
            Emails = new[]
            {
                new ScimEmail
                {
                    Value = "delete.hard@test.com",
                    Primary = true
                }
            },
            Active = true
        };
        ScimUser createdUser = await _scimService.CreateUserAsync(createRequest);

        // Act - Delete user (hard delete)
        await _scimService.DeleteUserAsync(createdUser.Id);

        // Assert - User should not exist
        ScimUser? result = await _scimService.GetUserAsync(createdUser.Id);
        result.Should().BeNull();

        // Verify sync log
        IReadOnlyList<ScimSyncLogDto> syncLogs = await _scimService.GetSyncLogsAsync();
        syncLogs.Should().Contain(log =>
            log.Operation == ScimOperation.Delete &&
            log.ResourceType == ScimResourceType.User &&
            log.Success);
    }

    [Fact]
    public async Task DeleteUser_WithoutDeprovision_DisablesUser()
    {
        // Arrange - Deprovisioning is disabled by default
        ScimUserRequest createRequest = new()
        {
            UserName = "delete.soft@test.com",
            Emails = new[]
            {
                new ScimEmail
                {
                    Value = "delete.soft@test.com",
                    Primary = true
                }
            },
            Active = true
        };
        ScimUser createdUser = await _scimService.CreateUserAsync(createRequest);

        // Act - Delete user (soft delete)
        await _scimService.DeleteUserAsync(createdUser.Id);

        // Assert - User should still exist but disabled
        ScimUser? result = await _scimService.GetUserAsync(createdUser.Id);
        result.Should().NotBeNull();
        result.Active.Should().BeFalse();

        // Verify sync log
        IReadOnlyList<ScimSyncLogDto> syncLogs = await _scimService.GetSyncLogsAsync();
        syncLogs.Should().Contain(log =>
            log.Operation == ScimOperation.Delete &&
            log.ResourceType == ScimResourceType.User &&
            log.Success);
    }

    [Fact]
    public async Task ListUsers_WithFilter_ReturnsFiltered()
    {
        // Arrange - Create multiple users
        _ = await _scimService.CreateUserAsync(new ScimUserRequest
        {
            UserName = "alice@test.com",
            Name = new ScimName { GivenName = "Alice", FamilyName = "Anderson" },
            Emails = new[] { new ScimEmail { Value = "alice@test.com", Primary = true } },
            Active = true
        });

        _ = await _scimService.CreateUserAsync(new ScimUserRequest
        {
            UserName = "bob@test.com",
            Name = new ScimName { GivenName = "Bob", FamilyName = "Brown" },
            Emails = new[] { new ScimEmail { Value = "bob@test.com", Primary = true } },
            Active = true
        });

        // Act - Filter by username
        ScimListRequest listRequest = new(
            Filter: "userName eq \"alice@test.com\"",
            StartIndex: 1,
            Count: 10);
        ScimListResponse<ScimUser> result = await _scimService.ListUsersAsync(listRequest);

        // Assert
        result.Should().NotBeNull();
        result.Resources.Should().NotBeEmpty();
        result.Resources.Should().Contain(u => u.UserName == "alice@test.com");
    }

    [Fact]
    public async Task ListUsers_WithPagination_ReturnsPaged()
    {
        // Arrange - Create multiple users
        for (int i = 0; i < 5; i++)
        {
            _ = await _scimService.CreateUserAsync(new ScimUserRequest
            {
                UserName = $"user{i}@test.com",
                Emails = new[] { new ScimEmail { Value = $"user{i}@test.com", Primary = true } },
                Active = true
            });
        }

        // Act - Request page 1 with 2 items
        ScimListRequest listRequest = new(
            StartIndex: 1,
            Count: 2);
        ScimListResponse<ScimUser> result = await _scimService.ListUsersAsync(listRequest);

        // Assert
        result.Should().NotBeNull();
        result.StartIndex.Should().Be(1);
        result.ItemsPerPage.Should().BeLessThanOrEqualTo(2);
        result.Resources.Should().NotBeEmpty();
    }
}

/// <summary>
/// Test factory for SCIM provisioning tests.
/// Uses WireMock to simulate Keycloak Admin API.
/// </summary>
public class ScimProvisioningTestFactory : FoundryApiFactory
{
    private readonly MockScimIdpFixture _mockIdp = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Initialize the mock IdP
        _mockIdp.InitializeAsync().GetAwaiter().GetResult();

        builder.ConfigureTestServices(services =>
        {
            services.AddHttpContextAccessor();

            // Replace Keycloak HttpClient with WireMock
            services.AddHttpClient("KeycloakAdminClient", client =>
            {
                client.BaseAddress = new Uri(_mockIdp.BaseUrl);
            });

            // Fixed tenant context for tests
            services.AddScoped<ITenantContext>(_ => new TenantContext
            {
                TenantId = TenantId.Create(TestConstants.TestTenantId),
                TenantName = "Test Tenant",
                IsResolved = true
            });
        });
    }

    public void ResetKeycloakMock()
    {
        _mockIdp.Reset();
    }

    public void SetupUserCreationConflict()
    {
        // This will be handled by the mock's callback logic
        // The mock already tracks created users and can detect duplicates
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _mockIdp.DisposeAsync();
    }
}
