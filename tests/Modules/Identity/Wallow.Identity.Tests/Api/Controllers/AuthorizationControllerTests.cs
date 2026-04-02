using System.Collections.Immutable;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;

#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

namespace Wallow.Identity.Tests.Api.Controllers;

public sealed class AuthorizationControllerTests : IDisposable
{
    private static readonly string _testUserId = Guid.NewGuid().ToString();
    private const string ThirdPartyClientId = "my-external-app";
    private const string FirstPartyClientId = "wallow-web";
    private const string ApplicationId = "app-id-123";

    private readonly UserManager<WallowUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictAuthorizationManager _authorizationManager;
    private readonly IClientTenantResolver _clientTenantResolver;
    private readonly IOrganizationService _organizationService;
    private readonly AuthorizationController _controller;

    public AuthorizationControllerTests()
    {
        IUserStore<WallowUser> userStore = Substitute.For<IUserStore<WallowUser>>();
        _userManager = Substitute.For<UserManager<WallowUser>>(
            userStore, null, null, null, null, null, null, null, null);

        _configuration = Substitute.For<IConfiguration>();
        _configuration["AuthUrl"].Returns("https://auth.example.com");

        _applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        _authorizationManager = Substitute.For<IOpenIddictAuthorizationManager>();
        _clientTenantResolver = Substitute.For<IClientTenantResolver>();
        _organizationService = Substitute.For<IOrganizationService>();

        _controller = new AuthorizationController(
            _userManager,
            _configuration,
            _applicationManager,
            _authorizationManager,
            _clientTenantResolver,
            _organizationService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthorizationController>.Instance);
    }

    public void Dispose()
    {
        _controller.Dispose();
        _userManager.Dispose();
    }

    private void SetupAuthenticatedHttpContext(OpenIddictRequest request, string? queryString = null)
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, _testUserId)
        ], "test"));

        DefaultHttpContext httpContext = new() { User = user };

        // Set up the OpenIddict server transaction on the feature collection
        OpenIddictServerTransaction transaction = new() { Request = request };
        httpContext.Features.Set(new OpenIddictServerAspNetCoreFeature { Transaction = transaction });

        httpContext.Request.Path = "/connect/authorize";
        httpContext.Request.QueryString = new QueryString(queryString ?? "?client_id=" + (request.ClientId ?? ThirdPartyClientId));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // Mock Url helper for IsLocalUrl
        IUrlHelper urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.IsLocalUrl(Arg.Any<string>()).Returns(true);
        _controller.Url = urlHelper;
    }

    private void SetupUser()
    {
        WallowUser wallowUser = WallowUser.Create(
            Guid.NewGuid(), "Test", "User", "test@example.com", TimeProvider.System);

        _userManager.GetUserId(Arg.Any<ClaimsPrincipal>()).Returns(_testUserId);
        _userManager.FindByIdAsync(_testUserId).Returns(wallowUser);
        _userManager.GetUserNameAsync(wallowUser).Returns("testuser");
        _userManager.GetEmailAsync(wallowUser).Returns("test@example.com");
        _userManager.GetRolesAsync(wallowUser).Returns(new List<string>());
        _userManager.GetClaimsAsync(wallowUser).Returns(new List<Claim>());
    }

    private void SetupApplication(string clientId, string applicationId = ApplicationId)
    {
        object application = new();
        _applicationManager.FindByClientIdAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<object?>(application));
        _applicationManager.GetClientIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(clientId));
        _applicationManager.GetIdAsync(application, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(applicationId));
    }

    private void SetupNoExistingAuthorizations()
    {
        _authorizationManager.FindBySubjectAsync(_testUserId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>());
    }

    private void SetupExistingValidAuthorization(string applicationId, ImmutableArray<string> scopes)
    {
        object authorization = new();
        _authorizationManager.FindBySubjectAsync(_testUserId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(authorization));
        _authorizationManager.GetApplicationIdAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(applicationId));
        _authorizationManager.GetStatusAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<string?>(OpenIddictConstants.Statuses.Valid));
        _authorizationManager.GetScopesAsync(authorization, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(scopes));
    }

    private void SetupClientTenantResolver(string clientId)
    {
        _clientTenantResolver.ResolveAsync(clientId, Arg.Any<CancellationToken>())
            .Returns((ClientTenantInfo?)null);
    }

    #region Consent Denied

    [Fact]
    public async Task Authorize_WithConsentDenied_ThirdPartyClient_ReturnsForbidWithConsentRequired()
    {
        // Arrange
        OpenIddictRequest request = new()
        {
            ClientId = ThirdPartyClientId,
            ["consent_denied"] = "true"
        };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupNoExistingAuthorizations();
        SetupClientTenantResolver(ThirdPartyClientId);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert - the controller should return a Forbid result with consent_required error
        // Currently the controller does NOT handle consent_denied, so this test will fail
        ForbidResult forbidResult = result.Should().BeOfType<ForbidResult>().Subject;
        forbidResult.AuthenticationSchemes.Should().Contain(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    #endregion

    #region Consent Granted - No Existing Authorization

    [Fact]
    public async Task Authorize_WithConsentGranted_NoExistingAuthorization_CreatesAuthorizationAndReturnsSignIn()
    {
        // Arrange
        OpenIddictRequest request = new()
        {
            ClientId = ThirdPartyClientId,
            Scope = "openid profile",
            ["consent_granted"] = "true"
        };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupNoExistingAuthorizations();
        SetupClientTenantResolver(ThirdPartyClientId);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert - with consent_granted and no valid authorization, the handler should
        // create a new authorization and return a SignInResult.
        // Currently the controller redirects to consent screen when no valid authorization exists,
        // regardless of consent_granted parameter. This test will fail.
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();
        await _authorizationManager.Received(1).CreateAsync(
            Arg.Any<OpenIddictAuthorizationDescriptor>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Consent Granted - Existing Valid Authorization

    [Fact]
    public async Task Authorize_WithConsentGranted_ExistingValidAuthorization_DoesNotCreateDuplicateAuthorization()
    {
        // Arrange
        OpenIddictRequest request = new()
        {
            ClientId = ThirdPartyClientId,
            Scope = "openid profile",
            ["consent_granted"] = "true"
        };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(ThirdPartyClientId);
        SetupExistingValidAuthorization(ApplicationId, ["openid", "profile"]);
        SetupClientTenantResolver(ThirdPartyClientId);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert - when there is already a valid authorization covering the requested scopes,
        // consent_granted should not create a duplicate. The controller currently always creates
        // a new authorization for third-party clients after the consent check. This test verifies
        // the duplicate-prevention path when consent_granted=true and hasValidAuthorization=true.
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();

        // Should NOT call CreateAsync because a valid authorization already exists
        await _authorizationManager.DidNotReceive().CreateAsync(
            Arg.Any<OpenIddictAuthorizationDescriptor>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region First-Party Client Skips Consent

    [Fact]
    public async Task Authorize_FirstPartyClient_WithConsentParameter_SkipsConsentLogicAndReturnsSignIn()
    {
        // Arrange
        OpenIddictRequest request = new()
        {
            ClientId = FirstPartyClientId,
            Scope = "openid profile",
            ["consent_granted"] = "true"
        };

        SetupAuthenticatedHttpContext(request);
        SetupUser();
        SetupApplication(FirstPartyClientId);
        SetupClientTenantResolver(FirstPartyClientId);

        // Act
        IActionResult result = await _controller.Authorize();

        // Assert - first-party clients (wallow-* prefix) skip consent entirely
        // and go directly to token issuance. No authorization should be created.
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.SignInResult>();

        // First-party clients should never trigger authorization creation
        await _authorizationManager.DidNotReceive().CreateAsync(
            Arg.Any<OpenIddictAuthorizationDescriptor>(), Arg.Any<CancellationToken>());

        // Consent-related authorization lookups should not happen for first-party clients
        _authorizationManager.DidNotReceive().FindBySubjectAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (T item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
