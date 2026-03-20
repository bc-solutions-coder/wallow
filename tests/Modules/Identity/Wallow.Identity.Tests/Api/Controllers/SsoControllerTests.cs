using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Wallow.Identity.Tests.Api.Controllers;

public class SsoControllerTests
{
    private readonly ISsoService _ssoService;
    private readonly SsoController _controller;

    public SsoControllerTests()
    {
        _ssoService = Substitute.For<ISsoService>();
        _controller = new SsoController(_ssoService);
    }

    #region GetConfiguration

    [Fact]
    public async Task GetConfiguration_WhenFound_ReturnsOk()
    {
        SsoConfigurationDto config = CreateSsoConfig();
        _ssoService.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(config);

        ActionResult<SsoConfigurationDto> result = await _controller.GetConfiguration(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<SsoConfigurationDto>();
    }

    [Fact]
    public async Task GetConfiguration_WhenNotFound_ReturnsNotFound()
    {
        _ssoService.GetConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns((SsoConfigurationDto?)null);

        ActionResult<SsoConfigurationDto> result = await _controller.GetConfiguration(CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region ConfigureOidc

    [Fact]
    public async Task ConfigureOidc_WithValidRequest_ReturnsOkWithConfig()
    {
        ConfigureOidcSsoRequest request = new(
            "Azure AD", "https://login.microsoft.com/tenant", "client-id", "client-secret");
        SsoConfigurationDto config = CreateSsoConfig();
        _ssoService.SaveOidcConfigurationAsync(Arg.Any<SaveOidcConfigRequest>(), Arg.Any<CancellationToken>())
            .Returns(config);

        ActionResult<SsoConfigurationDto> result = await _controller.ConfigureOidc(request, CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<SsoConfigurationDto>();
    }

    [Fact]
    public async Task ConfigureOidc_MapsApiRequestToApplicationRequest()
    {
        ConfigureOidcSsoRequest request = new(
            "Azure AD", "https://issuer", "cid", "csecret",
            "openid email", "email", "given_name", "family_name",
            "groups", true, false, "user", true);
        SsoConfigurationDto config = CreateSsoConfig();
        SaveOidcConfigRequest? capturedRequest = null;
        _ssoService.SaveOidcConfigurationAsync(Arg.Do<SaveOidcConfigRequest>(r => capturedRequest = r), Arg.Any<CancellationToken>())
            .Returns(config);

        await _controller.ConfigureOidc(request, CancellationToken.None);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.DisplayName.Should().Be("Azure AD");
        capturedRequest.Issuer.Should().Be("https://issuer");
        capturedRequest.ClientId.Should().Be("cid");
        capturedRequest.ClientSecret.Should().Be("csecret");
        capturedRequest.Scopes.Should().Be("openid email");
        capturedRequest.GroupsAttribute.Should().Be("groups");
        capturedRequest.EnforceForAllUsers.Should().BeTrue();
        capturedRequest.AutoProvisionUsers.Should().BeFalse();
        capturedRequest.DefaultRole.Should().Be("user");
        capturedRequest.SyncGroupsAsRoles.Should().BeTrue();
    }

    #endregion

    #region TestConnection

    [Fact]
    public async Task TestConnection_ReturnsOkWithResult()
    {
        SsoTestResult testResult = new(true, null);
        _ssoService.TestConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(testResult);

        ActionResult<SsoTestResult> result = await _controller.TestConnection(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        SsoTestResult response = ok.Value.Should().BeOfType<SsoTestResult>().Subject;
        response.Success.Should().BeTrue();
    }

    #endregion

    #region Activate

    [Fact]
    public async Task Activate_ReturnsNoContent()
    {
        IActionResult result = await _controller.Activate(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _ssoService.Received(1).ActivateAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region Disable

    [Fact]
    public async Task Disable_ReturnsNoContent()
    {
        IActionResult result = await _controller.Disable(CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        await _ssoService.Received(1).DisableAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetOidcCallbackInfo

    [Fact]
    public async Task GetOidcCallbackInfo_ReturnsOkWithInfo()
    {
        OidcCallbackInfo callbackInfo = new("https://app/callback", "https://app/logout", "client-id");
        _ssoService.GetOidcCallbackInfoAsync(Arg.Any<CancellationToken>())
            .Returns(callbackInfo);

        ActionResult<OidcCallbackInfo> result = await _controller.GetOidcCallbackInfo(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        OidcCallbackInfo response = ok.Value.Should().BeOfType<OidcCallbackInfo>().Subject;
        response.RedirectUri.Should().Be("https://app/callback");
        response.ClientId.Should().Be("client-id");
    }

    #endregion

    #region ValidateConfiguration

    [Fact]
    public async Task ValidateConfiguration_ReturnsOkWithResult()
    {
        SsoValidationResult validationResult = new(true, null, "entity-id", "https://idp/sso", DateTime.UtcNow.AddYears(1));
        _ssoService.ValidateIdpConfigurationAsync(Arg.Any<CancellationToken>())
            .Returns(validationResult);

        ActionResult<SsoValidationResult> result = await _controller.ValidateConfiguration(CancellationToken.None);

        OkObjectResult ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        SsoValidationResult response = ok.Value.Should().BeOfType<SsoValidationResult>().Subject;
        response.IsValid.Should().BeTrue();
        response.IdpEntityId.Should().Be("entity-id");
    }

    #endregion

    #region Helpers

    private static SsoConfigurationDto CreateSsoConfig()
    {
        return new SsoConfigurationDto(
            SsoConfigurationId.New(), "Test IdP", SsoProtocol.Saml, SsoStatus.Active,
            "entity-id", "https://idp/sso", true,
            null, null, false,
            false, true, null, false,
            "sp-entity-id", "https://sp/acs", "https://sp/metadata");
    }

    #endregion
}
