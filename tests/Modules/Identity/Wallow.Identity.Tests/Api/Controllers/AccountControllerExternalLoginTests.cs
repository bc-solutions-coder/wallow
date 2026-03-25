using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wolverine;

namespace Wallow.Identity.Tests.Api.Controllers;

public class AccountControllerExternalLoginTests
{
    private readonly AccountController _controller;
    private readonly SignInManager<WallowUser> _signInManager;
    private readonly IRedirectUriValidator _redirectUriValidator;
    private readonly IAuthenticationSchemeProvider _authSchemeProvider;

    public AccountControllerExternalLoginTests()
    {
        _signInManager = Substitute.For<SignInManager<WallowUser>>(
            Substitute.For<UserManager<WallowUser>>(
                Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null),
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<WallowUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<ILogger<SignInManager<WallowUser>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<WallowUser>>());

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthUrl"] = "http://localhost:5002"
            })
            .Build();

        _redirectUriValidator = Substitute.For<IRedirectUriValidator>();
        _authSchemeProvider = Substitute.For<IAuthenticationSchemeProvider>();
        IDataProtectionProvider dataProtectionProvider = Substitute.For<IDataProtectionProvider>();

        _controller = new AccountController(
            _signInManager,
            configuration,
            _redirectUriValidator,
            dataProtectionProvider,
            _authSchemeProvider,
            Substitute.For<IMessageBus>(),
            Substitute.For<IClientTenantResolver>(),
            Substitute.For<IOrganizationService>(),
            Substitute.For<IPasswordlessService>(),
            Substitute.For<IMfaExemptionChecker>(),
            Substitute.For<IMfaService>(),
            Substitute.For<ILogger<AccountController>>(),
            TimeProvider.System);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        IUrlHelper urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.Action(Arg.Any<UrlActionContext>()).Returns("http://localhost:5000/api/v1/identity/auth/external-login-callback?returnUrl=http://localhost:5002");
        _controller.Url = urlHelper;
    }

    [Fact]
    public async Task ExternalLogin_WithEmptyProvider_ReturnsBadRequest()
    {
        IActionResult result = await _controller.ExternalLogin("", "http://localhost:5002");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExternalLogin_WithUnregisteredProvider_ReturnsBadRequest()
    {
        _authSchemeProvider.GetSchemeAsync("FakeProvider")
            .Returns((AuthenticationScheme?)null);

        IActionResult result = await _controller.ExternalLogin("FakeProvider", "http://localhost:5002");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ExternalLogin_WithInvalidReturnUrl_RedirectsToError()
    {
        _authSchemeProvider.GetSchemeAsync("Google")
            .Returns(new AuthenticationScheme("Google", "Google", typeof(IAuthenticationHandler)));
        _redirectUriValidator.IsAllowedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        IActionResult result = await _controller.ExternalLogin("Google", "http://evil.com");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("error?reason=invalid_redirect_uri");
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithoutAcceptedTerms_RedirectsToAcceptTerms()
    {
        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: false,
            returnUrl: "http://localhost:5002");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("accept-terms?error=terms_required");
    }

    [Fact]
    public async Task CompleteExternalRegistration_WithNoCookie_RedirectsToSessionExpired()
    {
        IActionResult result = await _controller.CompleteExternalRegistration(
            acceptedTerms: true,
            returnUrl: "http://localhost:5002");

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Contain("error=session_expired");
    }

    [Fact]
    public async Task GetExternalProviders_ReturnsOnlySignInManagerExternalSchemes()
    {
        List<AuthenticationScheme> schemes = new()
        {
            new AuthenticationScheme("TestProvider", "Test Provider", typeof(IAuthenticationHandler))
        };
        _signInManager.GetExternalAuthenticationSchemesAsync()
            .Returns(schemes);

        IActionResult result = await _controller.GetExternalProviders();

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        List<string> providers = okResult.Value.Should().BeOfType<List<string>>().Subject;
        providers.Should().ContainSingle().Which.Should().Be("Test Provider");
    }

    [Fact]
    public async Task GetExternalProviders_ExcludesProvidersNotReturnedBySignInManager()
    {
        _signInManager.GetExternalAuthenticationSchemesAsync()
            .Returns(new List<AuthenticationScheme>());

        IActionResult result = await _controller.GetExternalProviders();

        OkObjectResult okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        List<string> providers = okResult.Value.Should().BeOfType<List<string>>().Subject;
        providers.Should().BeEmpty();
    }
}
