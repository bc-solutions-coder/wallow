using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Wallow.Identity.Tests.Api.Controllers;

public class ServiceAccountsControllerTests
{
    private static readonly string[] _billingReadScope = ["billing:read"];
    private static readonly string[] _billingReadWriteScopes = ["billing:read", "billing:write"];
    private static readonly string[] _singleScope = ["scope1"];
    private readonly IServiceAccountService _serviceAccountService;
    private readonly ServiceAccountsController _controller;

    public ServiceAccountsControllerTests()
    {
        _serviceAccountService = Substitute.For<IServiceAccountService>();
        _controller = new ServiceAccountsController(_serviceAccountService);
    }

    #region List

    [Fact]
    public async Task List_ReturnsOkWithServiceAccounts()
    {
        ServiceAccountMetadataId id = ServiceAccountMetadataId.New();
        List<ServiceAccountDto> accounts =
        [
            new ServiceAccountDto(id, "client-1", "Backend Service", "Desc", ServiceAccountStatus.Active,
                _billingReadScope, DateTime.UtcNow, null)
        ];
        _serviceAccountService.ListAsync(Arg.Any<CancellationToken>())
            .Returns(accounts);

        ActionResult<IReadOnlyList<ServiceAccountDto>> result = await _controller.List(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        IReadOnlyList<ServiceAccountDto> response = ok.Value.Should().BeAssignableTo<IReadOnlyList<ServiceAccountDto>>().Subject;
        response.Should().HaveCount(1);
        response[0].ClientId.Should().Be("client-1");
    }

    #endregion

    #region Create

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreated()
    {
        ServiceAccountMetadataId newId = ServiceAccountMetadataId.New();
        Wallow.Identity.Api.Contracts.Requests.CreateServiceAccountRequest request = new("Backend", "Backend service", _billingReadScope);
        ServiceAccountCreatedResult createdResult = new(newId, "client-backend", "secret-123", "https://kc/token", _billingReadScope);
        _serviceAccountService.CreateAsync(Arg.Any<Wallow.Identity.Application.DTOs.CreateServiceAccountRequest>(), Arg.Any<CancellationToken>())
            .Returns(createdResult);

        ActionResult<ServiceAccountCreatedResponse> result = await _controller.Create(request, CancellationToken.None);

        CreatedAtActionResult created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(ServiceAccountsController.Get));
        ServiceAccountCreatedResponse response = created.Value.Should().BeOfType<ServiceAccountCreatedResponse>().Subject;
        response.ClientId.Should().Be("client-backend");
        response.ClientSecret.Should().Be("secret-123");
        response.TokenEndpoint.Should().Be("https://kc/token");
        response.Id.Should().Be(newId.Value.ToString());
    }

    [Fact]
    public async Task Create_MapsApiRequestToApplicationRequest()
    {
        ServiceAccountMetadataId newId = ServiceAccountMetadataId.New();
        Wallow.Identity.Api.Contracts.Requests.CreateServiceAccountRequest request = new("Svc", "Description", _singleScope);
        ServiceAccountCreatedResult createdResult = new(newId, "c1", "s1", "t1", _singleScope);
        Wallow.Identity.Application.DTOs.CreateServiceAccountRequest? capturedRequest = null;
        _serviceAccountService.CreateAsync(Arg.Do<Wallow.Identity.Application.DTOs.CreateServiceAccountRequest>(r => capturedRequest = r), Arg.Any<CancellationToken>())
            .Returns(createdResult);

        await _controller.Create(request, CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Name.Should().Be("Svc");
        capturedRequest.Description.Should().Be("Description");
        capturedRequest.Scopes.Should().Contain("scope1");
    }

    #endregion

    #region Get

    [Fact]
    public async Task Get_WhenFound_ReturnsOk()
    {
        Guid id = Guid.NewGuid();
        ServiceAccountDto account = new(ServiceAccountMetadataId.Create(id), "client-1", "Test", null,
            ServiceAccountStatus.Active, _billingReadScope, DateTime.UtcNow, null);
        _serviceAccountService.GetAsync(Arg.Is<ServiceAccountMetadataId>(x => x.Value == id), Arg.Any<CancellationToken>())
            .Returns(account);

        ActionResult<ServiceAccountDto> result = await _controller.Get(id, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ServiceAccountDto response = ok.Value.Should().BeOfType<ServiceAccountDto>().Subject;
        response.ClientId.Should().Be("client-1");
    }

    [Fact]
    public async Task Get_WhenNotFound_ReturnsNotFound()
    {
        Guid id = Guid.NewGuid();
        _serviceAccountService.GetAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns((ServiceAccountDto?)null);

        ActionResult<ServiceAccountDto> result = await _controller.Get(id, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region UpdateScopes

    [Fact]
    public async Task UpdateScopes_ReturnsNoContent()
    {
        Guid id = Guid.NewGuid();
        UpdateScopesRequest request = new(_billingReadWriteScopes);

        ActionResult result = await _controller.UpdateScopes(id, request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _serviceAccountService.Received(1).UpdateScopesAsync(
            Arg.Is<ServiceAccountMetadataId>(x => x.Value == id),
            Arg.Is<IReadOnlyList<string>>(s => s.Count == 2),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region RotateSecret

    [Fact]
    public async Task RotateSecret_ReturnsOkWithNewSecret()
    {
        Guid id = Guid.NewGuid();
        DateTime rotatedAt = DateTime.UtcNow;
        SecretRotatedResult rotateResult = new("new-secret-xyz", rotatedAt);
        _serviceAccountService.RotateSecretAsync(Arg.Is<ServiceAccountMetadataId>(x => x.Value == id), Arg.Any<CancellationToken>())
            .Returns(rotateResult);

        ActionResult<SecretRotatedResponse> result = await _controller.RotateSecret(id, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        SecretRotatedResponse response = ok.Value.Should().BeOfType<SecretRotatedResponse>().Subject;
        response.NewClientSecret.Should().Be("new-secret-xyz");
        response.RotatedAt.Should().Be(rotatedAt);
    }

    #endregion

    #region Revoke

    [Fact]
    public async Task Revoke_ReturnsNoContent()
    {
        Guid id = Guid.NewGuid();

        ActionResult result = await _controller.Revoke(id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _serviceAccountService.Received(1).RevokeAsync(
            Arg.Is<ServiceAccountMetadataId>(x => x.Value == id),
            Arg.Any<CancellationToken>());
    }

    #endregion
}
