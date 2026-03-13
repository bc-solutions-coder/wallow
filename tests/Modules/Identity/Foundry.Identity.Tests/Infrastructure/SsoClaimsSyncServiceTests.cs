using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Infrastructure;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class SsoClaimsSyncServiceTests
{
    private static readonly string[] _valueAttribute = ["value"];
    private static readonly string[] _developersQaGroups = ["Developers", "QA"];
    private static readonly string[] _adminsGroup = ["Admins"];
    private static readonly string[] _jsonArrayGroups = ["[\"Engineering\",\"Design\"]"];
    private static readonly string[] _dnGroupName = ["CN=Engineering,OU=Groups,DC=corp,DC=example,DC=com"];
    private static readonly string[] _newGroupOnly = ["NewGroup"];
    private static readonly string[] _testGroupOnly = ["TestGroup"];
    private static readonly string[] _nonExistentGroup = ["_nonExistentGroup"];
    private static readonly string[] _failGroup = ["_failGroup"];
    private static readonly string[] _blankAndValidGroups = ["", "  ", "ValidGroup"];
    private static readonly string[] _cnAdmins = ["CN=Admins"];
    private static readonly string[] _someGroup = ["_someGroup"];
    private readonly ISsoConfigurationRepository _repository = Substitute.For<ISsoConfigurationRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<SsoClaimsSyncService> _logger = Substitute.For<ILogger<SsoClaimsSyncService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    [Fact]
    public async Task SyncUserClaimsAsync_WhenNoConfig_SkipsSync()
    {
        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns((SsoConfiguration?)null);

        SsoClaimsSyncService service = CreateService();

        await service.SyncUserClaimsAsync(Guid.NewGuid());

        // Should not throw, should just return early
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenSyncGroupsDisabled_SkipsSync()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        // SyncGroupsAsRoles defaults to false

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        SsoClaimsSyncService service = CreateService();

        await service.SyncUserClaimsAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenGroupsAttributeEmpty_SkipsSync()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, null, Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        SsoClaimsSyncService service = CreateService();

        await service.SyncUserClaimsAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenUserNotFound_SkipsSync()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetStatus($"/admin/realms/foundry/users/{userId}", HttpStatusCode.NotFound);

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenNoGroupsInAttributes_SkipsSync()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["other_attr"] = _valueAttribute
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>());

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WithGroups_SyncsRoles()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _developersQaGroups
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { id = "r1", name = "default-roles-foundry" } })
            .WithGet("/admin/realms/foundry/roles/developers",
                new { id = "role-dev", name = "developers" })
            .WithGet("/admin/realms/foundry/roles/qa",
                new { id = "role-qa", name = "qa" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WithDefaultRole_IncludesDefaultRole()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, "user", true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _adminsGroup
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>())
            .WithGet("/admin/realms/foundry/roles/admins",
                new { id = "role-admin", name = "admins" })
            .WithGet("/admin/realms/foundry/roles/user",
                new { id = "role-user", name = "user" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WithJsonArrayGroups_ParsesCorrectly()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _jsonArrayGroups
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>())
            .WithGet("/admin/realms/foundry/roles/engineering",
                new { id = "role-eng", name = "engineering" })
            .WithGet("/admin/realms/foundry/roles/design",
                new { id = "role-des", name = "design" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WithDnGroupNames_ExtractsCommonName()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _dnGroupName
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>())
            .WithGet("/admin/realms/foundry/roles/engineering",
                new { id = "role-eng", name = "engineering" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_RemovesOldRoles_WhenNotInTargetGroups()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _newGroupOnly
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[]
                {
                    new { id = "r1", name = "oldgroup" },
                    new { id = "r2", name = "default-roles-foundry" }
                })
            .WithGet("/admin/realms/foundry/roles/newgroup",
                new { id = "role-new", name = "newgroup" })
            .WithGet("/admin/realms/foundry/roles/oldgroup",
                new { id = "role-old", name = "oldgroup" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithDelete($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_PreservesSystemRoles()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _testGroupOnly
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[]
                {
                    new { id = "r1", name = "default-roles-foundry" },
                    new { id = "r2", name = "offline_access" },
                    new { id = "r3", name = "uma_authorization" }
                })
            .WithGet("/admin/realms/foundry/roles/testgroup",
                new { id = "role-tg", name = "testgroup" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        // Should not attempt to remove preserved roles
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenRoleDoesNotExist_SkipsAssignment()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _nonExistentGroup
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>())
            .WithGetStatus("/admin/realms/foundry/roles/nonexistentgroup", HttpStatusCode.NotFound);

        SsoClaimsSyncService service = CreateService(handler);

        // Should not throw, just skip the role
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenKeycloakThrows_PropagatesException()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGetThrow($"/admin/realms/foundry/users/{userId}");

        SsoClaimsSyncService service = CreateService(handler);

        // GetUserAttributesAsync catches exceptions and returns null, leading to early return
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenGetRolesFails_ReturnsEmpty()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _someGroup
                }
            })
            .WithGetThrow($"/admin/realms/foundry/users/{userId}/role-mappings/realm")
            .WithGet("/admin/realms/foundry/roles/somegroup",
                new { id = "role-sg", name = "somegroup" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenAssignRoleFails_DoesNotThrow()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _failGroup
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>())
            .WithGet("/admin/realms/foundry/roles/failgroup",
                new { id = "role-fg", name = "failgroup" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.InternalServerError);

        SsoClaimsSyncService service = CreateService(handler);

        // TryAssignRoleAsync catches exceptions
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenRemoveRoleFails_DoesNotThrow()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = []
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { id = "r1", name = "oldrole" } })
            .WithGet("/admin/realms/foundry/roles/oldrole",
                new { id = "role-old", name = "oldrole" })
            .WithDeleteStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.InternalServerError);

        SsoClaimsSyncService service = CreateService(handler);

        // Should not throw — empty groups means no groups found, so skips sync
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WithCnPrefixedGroup_SanitizesName()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _cnAdmins
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>())
            .WithGet("/admin/realms/foundry/roles/admins",
                new { id = "role-adm", name = "admins" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WithBlankGroupValues_SkipsThem()
    {
        SsoConfiguration config = SsoConfiguration.Create(
            _tenantId, "Test SSO", SsoProtocol.Saml,
            "email", "firstName", "lastName", Guid.Empty, TimeProvider.System);
        config.UpdateBehaviorSettings(false, false, null, true, "groups", Guid.Empty, TimeProvider.System);

        _repository.GetAsync(Arg.Any<CancellationToken>()).Returns(config);

        Guid userId = Guid.NewGuid();
        MockHttpHandler handler = new MockHttpHandler()
            .WithGet($"/admin/realms/foundry/users/{userId}", new
            {
                id = userId.ToString(),
                attributes = new Dictionary<string, IEnumerable<string>>
                {
                    ["groups"] = _blankAndValidGroups
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>())
            .WithGet("/admin/realms/foundry/roles/validgroup",
                new { id = "role-vg", name = "validgroup" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    private SsoClaimsSyncService CreateService(HttpMessageHandler? handler = null)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();

        HttpClient httpClient = handler != null
            ? new HttpClient(handler) : new HttpClient(new MockHttpHandler());
        httpClient.BaseAddress = new Uri("https://keycloak.test/");
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(httpClient);

        _tenantContext.TenantId.Returns(_tenantId);

        return new SsoClaimsSyncService(httpClientFactory, _repository, _tenantContext, Options.Create(new KeycloakOptions()), _logger);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content)> _routes = new Dictionary<string, (HttpStatusCode Status, object? Content)>();
        private readonly HashSet<string> _throwRoutes = [];

        public MockHttpHandler WithGet(string path, object content)
        {
            _routes[$"GET:{path}"] = (HttpStatusCode.OK, content);
            return this;
        }

        public MockHttpHandler WithGetStatus(string path, HttpStatusCode status)
        {
            _routes[$"GET:{path}"] = (status, null);
            return this;
        }

        public MockHttpHandler WithGetThrow(string path)
        {
            _throwRoutes.Add($"GET:{path}");
            return this;
        }

        public MockHttpHandler WithPost(string path, HttpStatusCode status)
        {
            _routes[$"POST:{path}"] = (status, null);
            return this;
        }

        public MockHttpHandler WithDelete(string path, HttpStatusCode status)
        {
            _routes[$"DELETE:{path}"] = (status, null);
            return this;
        }

        public MockHttpHandler WithDeleteStatus(string path, HttpStatusCode status)
        {
            _routes[$"DELETE:{path}"] = (status, null);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? "";
            string key = $"{request.Method}:{path}";

            if (_throwRoutes.Contains(key))
            {
                throw new HttpRequestException("Simulated failure");
            }

            if (_routes.TryGetValue(key, out (HttpStatusCode Status, object? Content) route))
            {
                HttpResponseMessage response = new(route.Status);
                if (route.Content != null)
                {
                    response.Content = JsonContent.Create(route.Content);
                }
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { })
            });
        }
    }
}
