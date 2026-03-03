using System.Net;
using System.Net.Http.Json;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Entities;
using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Domain.Identity;
using Foundry.Identity.Infrastructure.Services;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Keycloak.AuthServices.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable CA2000 // HttpClient/HttpMessageHandler lifetime is managed by test framework

namespace Foundry.Identity.Tests.Infrastructure;

public class KeycloakServiceAccountServiceTests
{
    private static readonly string[] _oneScope = ["scope1"];
    private static readonly string[] _twoScopes = ["scope1", "scope2"];

    private readonly IServiceAccountRepository _repository = Substitute.For<IServiceAccountRepository>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ILogger<KeycloakServiceAccountService> _logger = Substitute.For<ILogger<KeycloakServiceAccountService>>();
    private readonly TenantId _testTenantId = TenantId.Create(Guid.NewGuid());

    [Fact]
    public async Task ListAsync_ReturnsAllTenantServiceAccounts()
    {
        // Arrange
        List<ServiceAccountMetadata> accounts =
        [
            ServiceAccountMetadata.Create(_testTenantId, "sa-client-1", "Account 1", null, Array.Empty<string>(), Guid.Empty),
            ServiceAccountMetadata.Create(_testTenantId, "sa-client-2", "Account 2", "Description", _oneScope, Guid.Empty)
        ];

        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(accounts);

        KeycloakServiceAccountService service = CreateService();

        // Act
        IReadOnlyList<ServiceAccountDto> result = await service.ListAsync();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Account 1");
        result[1].Name.Should().Be("Account 2");
        result[1].Description.Should().Be("Description");
    }

    [Fact]
    public async Task GetAsync_WithExistingId_ReturnsServiceAccountDto()
    {
        // Arrange
        ServiceAccountMetadata metadata = ServiceAccountMetadata.Create(
            _testTenantId,
            "sa-test-client",
            "Test Account",
            "Test Description",
            _twoScopes,
            Guid.Empty);
        metadata.MarkUsed();

        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(metadata);

        KeycloakServiceAccountService service = CreateService();

        // Act
        ServiceAccountDto? result = await service.GetAsync(metadata.Id);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Account");
        result.Description.Should().Be("Test Description");
        result.Status.Should().Be(ServiceAccountStatus.Active);
        result.Scopes.Should().BeEquivalentTo(_twoScopes);
        result.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns((ServiceAccountMetadata?)null);

        KeycloakServiceAccountService service = CreateService();

        // Act
        ServiceAccountDto? result = await service.GetAsync(ServiceAccountMetadataId.New());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RotateSecretAsync_WithNonExistentAccount_ThrowsEntityNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns((ServiceAccountMetadata?)null);

        KeycloakServiceAccountService service = CreateService();

        // Act
        Func<Task<SecretRotatedResult>> act = async () => await service.RotateSecretAsync(ServiceAccountMetadataId.New());

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task RevokeAsync_WithNonExistentAccount_ThrowsEntityNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns((ServiceAccountMetadata?)null);

        KeycloakServiceAccountService service = CreateService();

        // Act
        Func<Task> act = async () => await service.RevokeAsync(ServiceAccountMetadataId.New());

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task UpdateScopesAsync_WithNonExistentAccount_ThrowsEntityNotFoundException()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns((ServiceAccountMetadata?)null);

        KeycloakServiceAccountService service = CreateService();

        // Act
        Func<Task> act = async () => await service.UpdateScopesAsync(
            ServiceAccountMetadataId.New(),
            _oneScope);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    private KeycloakServiceAccountService CreateService(HttpMessageHandler? handler = null)
    {
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        HttpClient httpClient = handler != null
            ? new HttpClient(handler)
            : new HttpClient(new MockHttpMessageHandler());
        httpClient.BaseAddress = new Uri("https://keycloak.test/");
        httpClientFactory.CreateClient("KeycloakAdminClient").Returns(httpClient);

        _tenantContext.TenantId.Returns(_testTenantId);

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

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Return a basic success response for any request
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = Guid.NewGuid().ToString(), value = "test-secret" })
            };
            response.Headers.Location = new Uri("https://keycloak.test/clients/" + Guid.NewGuid());
            return Task.FromResult(response);
        }
    }
}
