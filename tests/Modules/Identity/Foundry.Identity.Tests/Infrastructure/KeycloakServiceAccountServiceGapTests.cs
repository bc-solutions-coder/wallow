using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Identity;
using Foundry.Identity.Infrastructure;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Services;
using Keycloak.AuthServices.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakServiceAccountServiceGapTests
{
    private static readonly string[] _defaultScopes = ["read", "write"];

    private readonly IServiceAccountRepository _repository = Substitute.For<IServiceAccountRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ILogger<KeycloakServiceAccountService> _logger = Substitute.For<ILogger<KeycloakServiceAccountService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.Parse("aabbccdd-1122-3344-5566-778899001122"));

    // ── CreateAsync HTTP error paths ──────────────────────────────────────

    [Fact]
    public async Task CreateAsync_KeycloakReturnsError_ThrowsExternalServiceException()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/clients", HttpStatusCode.Conflict);

        KeycloakServiceAccountService service = CreateService(handler);

        CreateServiceAccountRequest request = new("Duplicate Service", null, _defaultScopes);

        Func<Task> act = async () => await service.CreateAsync(request);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task CreateAsync_GetSecretReturnsError_ThrowsExternalServiceException()
    {
        string internalClientId = Guid.NewGuid().ToString();

        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/clients", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/admin/realms/foundry/clients/{internalClientId}")
            .WithGet($"/admin/realms/foundry/clients/{internalClientId}/client-secret",
                HttpStatusCode.InternalServerError);

        KeycloakServiceAccountService service = CreateService(handler);

        CreateServiceAccountRequest request = new("Test Service", null, []);

        Func<Task> act = async () => await service.CreateAsync(request);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task CreateAsync_GeneratesCorrectClientIdFormat()
    {
        string internalClientId = Guid.NewGuid().ToString();

        string? capturedClientId = null;
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/clients", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/admin/realms/foundry/clients/{internalClientId}")
            .WithGet($"/admin/realms/foundry/clients/{internalClientId}/client-secret",
                new { value = "secret-123" });
        handler.OnPost = (uri, body) => capturedClientId = ExtractClientIdFromJson(body);

        KeycloakServiceAccountService service = CreateService(handler);

        CreateServiceAccountRequest request = new("My Test Service!", "desc", _defaultScopes);

        ServiceAccountCreatedResult result = await service.CreateAsync(request);

        // ClientId format: sa-{first 8 chars of tenantId}-{slugified name}
        result.ClientId.Should().StartWith("sa-aabbccdd-");
        result.ClientId.Should().Contain("my-test-service");
    }

    [Fact]
    public async Task CreateAsync_SlugifiesSpecialCharactersInName()
    {
        string internalClientId = Guid.NewGuid().ToString();

        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/clients", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/admin/realms/foundry/clients/{internalClientId}")
            .WithGet($"/admin/realms/foundry/clients/{internalClientId}/client-secret",
                new { value = "secret" });

        KeycloakServiceAccountService service = CreateService(handler);

        CreateServiceAccountRequest request = new("API @ Service #1!!!", null, []);

        ServiceAccountCreatedResult result = await service.CreateAsync(request);

        result.ClientId.Should().NotContain("@");
        result.ClientId.Should().NotContain("#");
        result.ClientId.Should().NotContain("!");
        result.ClientId.Should().NotEndWith("-");
    }

    [Fact]
    public async Task CreateAsync_StoresMetadataWithCorrectValues()
    {
        string internalClientId = Guid.NewGuid().ToString();

        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/clients", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/admin/realms/foundry/clients/{internalClientId}")
            .WithGet($"/admin/realms/foundry/clients/{internalClientId}/client-secret",
                new { value = "secret-456" });

        KeycloakServiceAccountService service = CreateService(handler);

        CreateServiceAccountRequest request = new("Metadata Check", "Check desc", _defaultScopes);

        ServiceAccountCreatedResult result = await service.CreateAsync(request);

        _repository.Received(1).Add(Arg.Is<ServiceAccountMetadata>(m =>
            m.Name == "Metadata Check" &&
            m.Description == "Check desc"));
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        result.Id.Should().NotBe(default);
    }

    // ── RotateSecretAsync HTTP error paths ────────────────────────────────

    [Fact]
    public async Task RotateSecretAsync_KeycloakClientNotFound_ThrowsInvalidOperationException()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-test-client", "Test", null, [], Guid.Empty, TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/clients", Array.Empty<object>());

        KeycloakServiceAccountService service = CreateService(handler);

        Func<Task> act = async () => await service.RotateSecretAsync(metadata.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task RotateSecretAsync_KeycloakReturnsErrorOnRotation_ThrowsExternalServiceException()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-test-client", "Test", null, [], Guid.Empty, TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        string internalClientId = Guid.NewGuid().ToString();

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/clients",
                new[] { new { id = internalClientId, clientId = "sa-test-client" } })
            .WithPost($"/admin/realms/foundry/clients/{internalClientId}/client-secret",
                HttpStatusCode.InternalServerError);

        KeycloakServiceAccountService service = CreateService(handler);

        Func<Task> act = async () => await service.RotateSecretAsync(metadata.Id);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task RotateSecretAsync_LookupReturnsError_ThrowsExternalServiceException()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-test-client", "Test", null, [], Guid.Empty, TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/clients", HttpStatusCode.Forbidden);

        KeycloakServiceAccountService service = CreateService(handler);

        Func<Task> act = async () => await service.RotateSecretAsync(metadata.Id);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    // ── UpdateScopesAsync HTTP error paths ────────────────────────────────

    [Fact]
    public async Task UpdateScopesAsync_KeycloakClientNotFound_ThrowsInvalidOperationException()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-test-client", "Test", null, ["old"], Guid.Empty, TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/clients", Array.Empty<object>());

        KeycloakServiceAccountService service = CreateService(handler);

        Func<Task> act = async () => await service.UpdateScopesAsync(metadata.Id, ["new-scope"]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UpdateScopesAsync_KeycloakReturnsErrorOnPut_ThrowsExternalServiceException()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-test-client", "Test", null, ["old"], Guid.Empty, TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        string internalClientId = Guid.NewGuid().ToString();

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/clients",
                new[] { new { id = internalClientId, clientId = "sa-test-client" } })
            .WithPut($"/admin/realms/foundry/clients/{internalClientId}", HttpStatusCode.Forbidden);

        KeycloakServiceAccountService service = CreateService(handler);

        Func<Task> act = async () => await service.UpdateScopesAsync(metadata.Id, ["new-scope"]);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    // ── RevokeAsync HTTP error paths ──────────────────────────────────────

    [Fact]
    public async Task RevokeAsync_KeycloakReturnsErrorOnDelete_ThrowsExternalServiceException()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-test-client", "Test", null, [], Guid.Empty, TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        string internalClientId = Guid.NewGuid().ToString();

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/clients",
                new[] { new { id = internalClientId, clientId = "sa-test-client" } })
            .WithDelete($"/admin/realms/foundry/clients/{internalClientId}", HttpStatusCode.InternalServerError);

        KeycloakServiceAccountService service = CreateService(handler);

        Func<Task> act = async () => await service.RevokeAsync(metadata.Id);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task RevokeAsync_LookupReturnsError_ThrowsExternalServiceException()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-test-client", "Test", null, [], Guid.Empty, TimeProvider.System);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/clients", HttpStatusCode.Unauthorized);

        KeycloakServiceAccountService service = CreateService(handler);

        Func<Task> act = async () => await service.RevokeAsync(metadata.Id);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    // ── GetAsync field mapping ────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_MapsAllFieldsCorrectly()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-full-map", "Full Map Account", "Full description",
            _defaultScopes, Guid.NewGuid(), TimeProvider.System);

        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        KeycloakServiceAccountService service = CreateService();

        ServiceAccountDto? result = await service.GetAsync(metadata.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(metadata.Id);
        result.ClientId.Should().Be("sa-full-map");
        result.Name.Should().Be("Full Map Account");
        result.Description.Should().Be("Full description");
        result.Scopes.Should().BeEquivalentTo(_defaultScopes);
        result.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        result.LastUsedAt.Should().BeNull();
    }

    // ── ListAsync field mapping ──────────────────────────────────────────

    [Fact]
    public async Task ListAsync_MapsAllFieldsCorrectly()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-list-map", "List Map", "desc",
            _defaultScopes, Guid.NewGuid(), TimeProvider.System);
        metadata.MarkUsed(TimeProvider.System);

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ServiceAccountMetadata> { metadata });

        KeycloakServiceAccountService service = CreateService();

        IReadOnlyList<ServiceAccountDto> result = await service.ListAsync();

        result.Should().HaveCount(1);
        ServiceAccountDto dto = result[0];
        dto.Id.Should().Be(metadata.Id);
        dto.ClientId.Should().Be("sa-list-map");
        dto.Name.Should().Be("List Map");
        dto.Description.Should().Be("desc");
        dto.Scopes.Should().BeEquivalentTo(_defaultScopes);
        dto.LastUsedAt.Should().NotBeNull();
    }

    // ── CreateAsync with null UserId ──────────────────────────────────────

    [Fact]
    public async Task CreateAsync_NullUserId_UsesGuidEmpty()
    {
        string internalClientId = Guid.NewGuid().ToString();

        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/clients", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/admin/realms/foundry/clients/{internalClientId}")
            .WithGet($"/admin/realms/foundry/clients/{internalClientId}/client-secret",
                new { value = "secret" });

        _currentUserService.UserId.Returns((Guid?)null);

        KeycloakServiceAccountService service = CreateService(handler, skipUserSetup: true);

        CreateServiceAccountRequest request = new("Null User Service", null, []);

        ServiceAccountCreatedResult result = await service.CreateAsync(request);

        _repository.Received(1).Add(Arg.Any<ServiceAccountMetadata>());
        result.Should().NotBeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private KeycloakServiceAccountService CreateService(HttpMessageHandler? handler = null, bool skipUserSetup = false)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = handler != null
            ? new HttpClient(handler) : new HttpClient(new MockHttpHandler());
        httpClient.BaseAddress = new Uri("https://keycloak.test/");
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(httpClient);

        _tenantContext.TenantId.Returns(_tenantId);

        if (!skipUserSetup)
        {
            _currentUserService.UserId.Returns(Guid.NewGuid());
        }

        IOptions<KeycloakAuthenticationOptions> options = Options.Create(new KeycloakAuthenticationOptions
        {
            AuthServerUrl = "https://keycloak.test"
        });

        return new KeycloakServiceAccountService(
            httpClientFactory,
            _repository,
            _tenantContext,
            _currentUserService,
            options,
            Options.Create(new KeycloakOptions()),
            TimeProvider.System,
            _logger);
    }

    private static string? ExtractClientIdFromJson(string? body)
    {
        if (body is null)
        {
            return null;
        }

        int idx = body.IndexOf("\"clientId\":", StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }
        int start = body.IndexOf('"', idx + 11) + 1;
        int end = body.IndexOf('"', start);
        return body[start..end];
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader)> _routes = new();

        public Action<string, string?>? OnPost { get; set; }

        public MockHttpHandler WithGet(string path, object content)
        {
            _routes[$"GET:{path}"] = (HttpStatusCode.OK, content, null);
            return this;
        }

        public MockHttpHandler WithGet(string path, HttpStatusCode status)
        {
            _routes[$"GET:{path}"] = (status, null, null);
            return this;
        }

        public MockHttpHandler WithPost(string path, HttpStatusCode status, string? locationHeader = null, object? content = null)
        {
            _routes[$"POST:{path}"] = (status, content, locationHeader);
            return this;
        }

        public MockHttpHandler WithPut(string path, HttpStatusCode status)
        {
            _routes[$"PUT:{path}"] = (status, null, null);
            return this;
        }

        public MockHttpHandler WithDelete(string path, HttpStatusCode status)
        {
            _routes[$"DELETE:{path}"] = (status, null, null);
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? "";
            string key = $"{request.Method}:{path}";

            if (request.Method == HttpMethod.Post && OnPost != null)
            {
                string? body = request.Content != null
                    ? await request.Content.ReadAsStringAsync(cancellationToken)
                    : null;
                OnPost(path, body);
            }

            if (_routes.TryGetValue(key, out (HttpStatusCode Status, object? Content, string? LocationHeader) route))
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
                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { })
            };
        }
    }
}
