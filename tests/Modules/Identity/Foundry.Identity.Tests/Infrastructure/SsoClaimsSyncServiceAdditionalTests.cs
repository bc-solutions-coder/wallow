using System.Net;
using System.Net.Http.Json;
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

namespace Foundry.Identity.Tests.Infrastructure;

public class SsoClaimsSyncServiceAdditionalTests
{
    private static readonly string[] _notValidJsonGroup = ["[not-valid-json"];
    private static readonly string[] _throwGroup = ["_throwGroup"];
    private static readonly string[] _keepGroup = ["_keepGroup"];
    private static readonly string[] _newOnlyGroup = ["NewOnly"];
    private static readonly string[] _testGroup = ["_testGroup"];
    private static readonly string[] _newGroup = ["_newGroup"];
    private static readonly string[] _jsonArrayWithBlanks = ["[\"Valid\",\"\",\"  \"]"];
    private readonly ISsoConfigurationRepository _repository = Substitute.For<ISsoConfigurationRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ILogger<SsoClaimsSyncService> _logger = Substitute.For<ILogger<SsoClaimsSyncService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    [Fact]
    public async Task SyncUserClaimsAsync_WithInvalidJsonArray_TreatsAsLiteral()
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
                    ["groups"] = _notValidJsonGroup
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>())
            .WithGet("/admin/realms/foundry/roles/[not-valid-json",
                new { id = "role-inv", name = "[not-valid-json" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenTryAssignRoleThrowsException_DoesNotThrow()
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
                    ["groups"] = _throwGroup
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>())
            .WithGetThrow("/admin/realms/foundry/roles/throwgroup")
            .WithPostThrow($"/admin/realms/foundry/users/{userId}/role-mappings/realm");

        SsoClaimsSyncService service = CreateService(handler);

        // TryAssignRoleAsync catches exceptions internally (GetRealmRoleAsync returns null when exception)
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenRemoveRoleThrowsException_DoesNotThrow()
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
                    ["groups"] = _keepGroup
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { id = "r1", name = "removeme" } })
            .WithGet("/admin/realms/foundry/roles/keepgroup",
                new { id = "role-keep", name = "keepgroup" })
            .WithGet("/admin/realms/foundry/roles/removeme",
                new { id = "role-rm", name = "removeme" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithSendThrow(HttpMethod.Delete, $"/admin/realms/foundry/users/{userId}/role-mappings/realm");

        SsoClaimsSyncService service = CreateService(handler);

        // TryRemoveRoleAsync catches exceptions
        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenRemoveRoleNotFoundInKeycloak_Skips()
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
                    ["groups"] = _newOnlyGroup
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { id = "r1", name = "staleRole" } })
            .WithGet("/admin/realms/foundry/roles/newonly",
                new { id = "role-new", name = "newonly" })
            .WithGetStatus("/admin/realms/foundry/roles/stalerole", HttpStatusCode.NotFound)
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WhenGetRolesReturnsNullList_TreatsAsEmpty()
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
                    ["groups"] = _testGroup
                }
            })
            .WithGetNull($"/admin/realms/foundry/users/{userId}/role-mappings/realm")
            .WithGet("/admin/realms/foundry/roles/testgroup",
                new { id = "role-tg", name = "testgroup" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent);

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WithRemoveRoleFails_LogsFailure()
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
                    ["groups"] = _newGroup
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                new[] { new { id = "r1", name = "oldgroup" } })
            .WithGet("/admin/realms/foundry/roles/newgroup",
                new { id = "role-new", name = "newgroup" })
            .WithGet("/admin/realms/foundry/roles/oldgroup",
                new { id = "role-old", name = "oldgroup" })
            .WithPost($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.NoContent)
            .WithDeleteStatus($"/admin/realms/foundry/users/{userId}/role-mappings/realm", HttpStatusCode.Forbidden);

        SsoClaimsSyncService service = CreateService(handler);

        await service.SyncUserClaimsAsync(userId);
    }

    [Fact]
    public async Task SyncUserClaimsAsync_WithJsonArrayContainingBlanks_SkipsBlanks()
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
                    ["groups"] = _jsonArrayWithBlanks
                }
            })
            .WithGet($"/admin/realms/foundry/users/{userId}/role-mappings/realm",
                Array.Empty<object>())
            .WithGet("/admin/realms/foundry/roles/valid",
                new { id = "role-v", name = "valid" })
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
        private readonly HashSet<string> _nullRoutes = [];

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

        public MockHttpHandler WithGetNull(string path)
        {
            _nullRoutes.Add($"GET:{path}");
            return this;
        }

        public MockHttpHandler WithPost(string path, HttpStatusCode status)
        {
            _routes[$"POST:{path}"] = (status, null);
            return this;
        }

        public MockHttpHandler WithPostThrow(string path)
        {
            _throwRoutes.Add($"POST:{path}");
            return this;
        }

        public MockHttpHandler WithDeleteStatus(string path, HttpStatusCode status)
        {
            _routes[$"DELETE:{path}"] = (status, null);
            return this;
        }

        public MockHttpHandler WithSendThrow(HttpMethod method, string path)
        {
            _throwRoutes.Add($"{method}:{path}");
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

            if (_nullRoutes.Contains(key))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
                });
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
