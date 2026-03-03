using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Identity;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Services;
using Keycloak.AuthServices.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakServiceAccountServiceAdditionalTests
{
    private static readonly string[] _invoicesReadWriteScopes = ["invoices.read", "invoices.write"];
    private static readonly string[] _oldScope = ["old-scope"];
    private readonly IServiceAccountRepository _repository = Substitute.For<IServiceAccountRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ILogger<KeycloakServiceAccountService> _logger = Substitute.For<ILogger<KeycloakServiceAccountService>>();
    private readonly TenantId _tenantId = TenantId.Create(Guid.Parse("12345678-1234-1234-1234-123456789abc"));

    [Fact]
    public async Task CreateAsync_Success_ReturnsResult()
    {
        string internalClientId = Guid.NewGuid().ToString();

        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/clients", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/admin/realms/foundry/clients/{internalClientId}")
            .WithGet($"/admin/realms/foundry/clients/{internalClientId}/client-secret",
                new { value = "generated-secret-123" });

        KeycloakServiceAccountService service = CreateService(handler);

        CreateServiceAccountRequest request = new(
            "Test Service",
            "A test service account",
            _invoicesReadWriteScopes);

        ServiceAccountCreatedResult result = await service.CreateAsync(request);

        result.Should().NotBeNull();
        result.ClientSecret.Should().Be("generated-secret-123");
        result.Scopes.Should().BeEquivalentTo(_invoicesReadWriteScopes);
        result.TokenEndpoint.Should().Contain("protocol/openid-connect/token");

        _repository.Received(1).Add(Arg.Any<ServiceAccountMetadata>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_MissingLocationHeader_Throws()
    {
        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/clients", HttpStatusCode.Created);

        KeycloakServiceAccountService service = CreateService(handler);

        CreateServiceAccountRequest request = new(
            "Test Service", null, Array.Empty<string>());

        Func<Task> act = async () => await service.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Location header*");
    }

    [Fact]
    public async Task CreateAsync_MissingClientSecret_Throws()
    {
        string internalClientId = Guid.NewGuid().ToString();

        MockHttpHandler handler = new MockHttpHandler()
            .WithPost("/admin/realms/foundry/clients", HttpStatusCode.Created,
                locationHeader: $"https://keycloak.test/admin/realms/foundry/clients/{internalClientId}")
            .WithGet($"/admin/realms/foundry/clients/{internalClientId}/client-secret",
                new { value = (string?)null });

        KeycloakServiceAccountService service = CreateService(handler);

        CreateServiceAccountRequest request = new(
            "Test Service", null, Array.Empty<string>());

        Func<Task> act = async () => await service.CreateAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*client secret*");
    }

    [Fact]
    public async Task RotateSecretAsync_Success_ReturnsNewSecret()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-test-client", "Test", null, Array.Empty<string>(), Guid.Empty);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        string internalClientId = Guid.NewGuid().ToString();

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/clients",
                new[] { new { id = internalClientId, clientId = "sa-test-client" } })
            .WithPost($"/admin/realms/foundry/clients/{internalClientId}/client-secret", HttpStatusCode.OK,
                content: new { value = "new-rotated-secret" });

        KeycloakServiceAccountService service = CreateService(handler);

        SecretRotatedResult result = await service.RotateSecretAsync(metadata.Id);

        result.Should().NotBeNull();
        result.NewClientSecret.Should().Be("new-rotated-secret");
    }

    [Fact]
    public async Task RotateSecretAsync_MissingSecret_Throws()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-test-client", "Test", null, Array.Empty<string>(), Guid.Empty);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        string internalClientId = Guid.NewGuid().ToString();

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/clients",
                new[] { new { id = internalClientId, clientId = "sa-test-client" } })
            .WithPost($"/admin/realms/foundry/clients/{internalClientId}/client-secret", HttpStatusCode.OK,
                content: new { value = (string?)null });

        KeycloakServiceAccountService service = CreateService(handler);

        Func<Task> act = async () => await service.RotateSecretAsync(metadata.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*client secret*");
    }

    [Fact]
    public async Task UpdateScopesAsync_Success_UpdatesLocalAndKeycloak()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-test-client", "Test", null, _oldScope, Guid.Empty);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        string internalClientId = Guid.NewGuid().ToString();
        string[] newScopes = new[] { "new-scope-1", "new-scope-2" };

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/clients",
                new[] { new { id = internalClientId, clientId = "sa-test-client" } })
            .WithPut($"/admin/realms/foundry/clients/{internalClientId}", HttpStatusCode.NoContent);

        KeycloakServiceAccountService service = CreateService(handler);

        await service.UpdateScopesAsync(metadata.Id, newScopes);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_Success_DeletesFromKeycloakAndRevokesLocal()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-test-client", "Test", null, Array.Empty<string>(), Guid.Empty);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        string internalClientId = Guid.NewGuid().ToString();

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/clients",
                new[] { new { id = internalClientId, clientId = "sa-test-client" } })
            .WithDelete($"/admin/realms/foundry/clients/{internalClientId}", HttpStatusCode.NoContent);

        KeycloakServiceAccountService service = CreateService(handler);

        await service.RevokeAsync(metadata.Id);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_WhenKeycloakClientNotFound_ThrowsInvalidOperationException()
    {
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _tenantId, "sa-test-client", "Test", null, Array.Empty<string>(), Guid.Empty);
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        MockHttpHandler handler = new MockHttpHandler()
            .WithGet("/admin/realms/foundry/clients", Array.Empty<object>());

        KeycloakServiceAccountService service = CreateService(handler);

        Func<Task> act = async () => await service.RevokeAsync(metadata.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task ListAsync_ReturnsEmptyList_WhenNoAccounts()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<ServiceAccountMetadata>());

        KeycloakServiceAccountService service = CreateService();

        IReadOnlyList<ServiceAccountDto> result = await service.ListAsync();

        result.Should().BeEmpty();
    }

    private KeycloakServiceAccountService CreateService(HttpMessageHandler? handler = null)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = handler != null
            ? new HttpClient(handler)
            : new HttpClient(new MockHttpHandler());
        httpClient.BaseAddress = new Uri("https://keycloak.test/");
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(httpClient);

        _tenantContext.TenantId.Returns(_tenantId);
        _currentUserService.UserId.Returns(Guid.NewGuid());

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
            _logger);
    }

    private sealed class MockHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, object? Content, string? LocationHeader)> _routes = [];

        public MockHttpHandler WithGet(string path, object content)
        {
            _routes[$"GET:{path}"] = (HttpStatusCode.OK, content, null);
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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri?.AbsolutePath ?? "";
            string key = $"{request.Method}:{path}";

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
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { })
            });
        }
    }
}
