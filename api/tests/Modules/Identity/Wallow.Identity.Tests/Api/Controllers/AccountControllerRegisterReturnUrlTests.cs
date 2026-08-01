using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Identity.Tests.Api.Controllers;

/// <summary>
/// What survives registration on the email-verification link, and what does not.
///
/// The invitation screen sends an anonymous visitor to /register carrying
/// returnUrl=/invitation?token=…, and that relative path is the only thing keeping the
/// invitation reachable once the address is verified. IRedirectUriValidator answers only for
/// absolute URIs, so local paths are admitted separately.
/// </summary>
public class AccountControllerRegisterReturnUrlTests
{
    private const string AuthUrl = "http://localhost:5002";
    private const string TestEmail = "register-returnurl@test.com";
    private const string TestPassword = "Password123!";
    private const string InvitationReturnUrl = "/invitation?token=invite-token-123";

    private readonly AccountController _controller;
    private readonly UserManager<WallowUser> _userManager;
    private readonly IRedirectUriValidator _redirectUriValidator;
    private readonly IMessageBus _messageBus;

    public AccountControllerRegisterReturnUrlTests()
    {
        _userManager = Substitute.For<UserManager<WallowUser>>(
            Substitute.For<IUserStore<WallowUser>>(), null, null, null, null, null, null, null, null);

        SignInManager<WallowUser> signInManager = Substitute.For<SignInManager<WallowUser>>(
            _userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<WallowUser>>(),
            Substitute.For<IOptions<IdentityOptions>>(),
            Substitute.For<ILogger<SignInManager<WallowUser>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<WallowUser>>());

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthUrl"] = AuthUrl
            })
            .Build();

        _redirectUriValidator = Substitute.For<IRedirectUriValidator>();
        _messageBus = Substitute.For<IMessageBus>();

        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(Substitute.For<IDatabase>());

        _controller = new AccountController(
            signInManager,
            configuration,
            _redirectUriValidator,
            new EphemeralDataProtectionProvider(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            _messageBus,
            Substitute.For<IClientTenantResolver>(),
            Substitute.For<IPasswordlessService>(),
            Substitute.For<IMfaExemptionChecker>(),
            Substitute.For<IMfaService>(),
            Substitute.For<IMfaPartialAuthService>(),
            Substitute.For<IOrganizationMfaPolicyService>(),
            Substitute.For<IMfaLockoutService>(),
            redis,
            Substitute.For<ILogger<AccountController>>(),
            TimeProvider.System);

        DefaultHttpContext httpContext = new();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Real UrlHelper so IsLocalUrl keeps its production semantics (relative-only).
        _controller.Url = new UrlHelper(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()));

        _userManager.CreateAsync(Arg.Any<WallowUser>(), TestPassword).Returns(IdentityResult.Success);
        _userManager.GenerateEmailConfirmationTokenAsync(Arg.Any<WallowUser>()).Returns("token123");

        // Nothing is allow-listed: every admitted returnUrl below is admitted for being local.
        _redirectUriValidator
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(false);
    }

    [Fact]
    public async Task Register_WithAnInvitationReturnUrl_PutsItOnTheVerificationLink()
    {
        await RegisterWithReturnUrlAsync(InvitationReturnUrl);

        await _messageBus.Received(1).PublishAsync(Arg.Is<EmailVerificationRequestedEvent>(e =>
            e.VerifyUrl.Contains(
                $"returnUrl={Uri.EscapeDataString(InvitationReturnUrl)}", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Register_WithAnInvitationReturnUrl_EncodesItAsOneParameter()
    {
        await RegisterWithReturnUrlAsync(InvitationReturnUrl);

        // The token's own `?` and `=` must not become further parameters on the verify link,
        // or the verification screen reads a truncated token and the invitation is unreachable.
        await _messageBus.Received(1).PublishAsync(Arg.Is<EmailVerificationRequestedEvent>(e =>
            !e.VerifyUrl.Contains("returnUrl=/invitation", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("//evil.example/invitation")]
    [InlineData("https://evil.example/invitation")]
    [InlineData("/\\evil.example/invitation")]
    public async Task Register_WithAReturnUrlThatLeavesThisSite_DropsIt(string returnUrl)
    {
        await RegisterWithReturnUrlAsync(returnUrl);

        await _messageBus.Received(1).PublishAsync(Arg.Is<EmailVerificationRequestedEvent>(e =>
            !e.VerifyUrl.Contains("returnUrl=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Register_WithAnAllowListedAbsoluteReturnUrl_StillPutsItOnTheLink()
    {
        const string absolute = "https://app.example.com/dashboard";
        _redirectUriValidator
            .IsAllowedAsync(absolute, Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await RegisterWithReturnUrlAsync(absolute);

        await _messageBus.Received(1).PublishAsync(Arg.Is<EmailVerificationRequestedEvent>(e =>
            e.VerifyUrl.Contains(
                $"returnUrl={Uri.EscapeDataString(absolute)}", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Register_WithAnInvitationReturnUrl_CreatesNoMembership()
    {
        await RegisterWithReturnUrlAsync(InvitationReturnUrl);

        // Being invited is authorization to join, but only once the address is proven. Acceptance
        // happens on the invitation screen after verification, never from this anonymous endpoint.
        await _messageBus.DidNotReceive().PublishAsync(Arg.Any<OrganizationMemberAddedEvent>());
    }

    private async Task RegisterWithReturnUrlAsync(string returnUrl)
    {
        IActionResult result = await _controller.Register(new AccountRegisterRequest(
            TestEmail, TestPassword, TestPassword, null, null, returnUrl));

        result.Should().BeOfType<OkObjectResult>();
    }
}
