using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Wallow.Identity.Api.Controllers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Extensions;

#pragma warning disable CA2012 // Use ValueTasks correctly - NSubstitute requires ValueTask in Returns()

namespace Wallow.Identity.Tests.Api.Controllers;

public sealed class LogoutControllerTests : IDisposable
{
    private const string TestSid = "sid-under-test";
    private static readonly Uri _issuer = new("https://id.example.com");

    private readonly IRedirectUriValidator _redirectUriValidator;
    private readonly IConfiguration _configuration;
    private readonly ISsoClientSessionService _ssoClientSessionService;
    private readonly IBackchannelLogoutNotifier _backchannelLogoutNotifier;
    private readonly IAccessRevoker _accessRevoker;
    private readonly IOptionsMonitor<OpenIddictServerOptions> _serverOptions;
    private readonly IAuthenticationService _authenticationService;
    private readonly LogoutController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public LogoutControllerTests()
    {
        _redirectUriValidator = Substitute.For<IRedirectUriValidator>();
        _redirectUriValidator
            .IsAllowedAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(Task.FromResult(true));

        _configuration = Substitute.For<IConfiguration>();
        _configuration["AuthUrl"].Returns("https://auth.example.com");

        _ssoClientSessionService = Substitute.For<ISsoClientSessionService>();
        _backchannelLogoutNotifier = Substitute.For<IBackchannelLogoutNotifier>();
        _accessRevoker = Substitute.For<IAccessRevoker>();

        _serverOptions = Substitute.For<IOptionsMonitor<OpenIddictServerOptions>>();
        _serverOptions.CurrentValue.Returns(new OpenIddictServerOptions { Issuer = _issuer });

        _authenticationService = Substitute.For<IAuthenticationService>();

        _controller = new LogoutController(
            _redirectUriValidator,
            _configuration,
            _ssoClientSessionService,
            _backchannelLogoutNotifier,
            _accessRevoker,
            _serverOptions,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LogoutController>.Instance);
    }

    public void Dispose() => _controller.Dispose();

    private void SetupHttpContext(string? sid, string queryString = "?client_id=wallow-web")
    {
        List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, _userId.ToString())];
        if (sid is not null)
        {
            claims.Add(new Claim(ClaimsPrincipalExtensions.SessionIdClaimType, sid));
        }

        ClaimsPrincipal user = new(new ClaimsIdentity(claims, "test"));
        DefaultHttpContext httpContext = new() { User = user };

        OpenIddictServerTransaction transaction = new() { Request = new OpenIddictRequest() };
        httpContext.Features.Set(new OpenIddictServerAspNetCoreFeature { Transaction = transaction });

        httpContext.Request.Path = "/connect/logout";
        httpContext.Request.QueryString = new QueryString(queryString);

        // The HttpContext.SignOutAsync extension resolves IAuthenticationService from
        // RequestServices, which is how these tests observe the identity-cookie sign-out.
        httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(_authenticationService)
            .BuildServiceProvider();

        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task Logout_WithoutSid_SignsOutBothSchemesImmediately()
    {
        // A session that never went through authorize has no participants to notify, so logout
        // stays a single round trip.
        SetupHttpContext(sid: null);

        IActionResult result = await _controller.Logout();

        SignOutResult signOut = result.Should().BeOfType<SignOutResult>().Subject;
        signOut.AuthenticationSchemes.Should()
            .ContainSingle(s => s == OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        await _authenticationService.Received(1).SignOutAsync(
            Arg.Any<HttpContext>(), IdentityConstants.ApplicationScheme, Arg.Any<AuthenticationProperties?>());
        await _ssoClientSessionService.DidNotReceive().BuildLogoutNotificationUrisAsync(
            Arg.Any<string>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>());
        await _accessRevoker.DidNotReceive().RevokeSessionAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithSid_RevokesTheSessionsTokens()
    {
        SetupHttpContext(TestSid);
        _ssoClientSessionService
            .BuildLogoutNotificationUrisAsync(TestSid, _issuer, Arg.Any<CancellationToken>())
            .Returns([]);

        await _controller.Logout();

        await _accessRevoker.Received(1).RevokeSessionAsync(
            _userId, TestSid, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutPost_RevokesTheSessionsTokens()
    {
        SetupHttpContext(TestSid);

        await _controller.LogoutPost();

        await _accessRevoker.Received(1).RevokeSessionAsync(
            _userId, TestSid, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithSid_NotifiesBackchannelBeforeForgettingTheSession()
    {
        SetupHttpContext(TestSid);
        _ssoClientSessionService
            .BuildLogoutNotificationUrisAsync(TestSid, _issuer, Arg.Any<CancellationToken>())
            .Returns([]);

        await _controller.Logout();

        // The notifier walks the participation rows itself, so it must run before ForgetAsync
        // deletes them — after, every logout would notify nobody.
        Received.InOrder(() =>
        {
            _backchannelLogoutNotifier.NotifyAsync(TestSid, _userId, _issuer, Arg.Any<CancellationToken>());
            _ssoClientSessionService.ForgetAsync(TestSid, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task LogoutPost_NotifiesBackchannelAndForgetsTheSession()
    {
        SetupHttpContext(TestSid);

        await _controller.LogoutPost();

        Received.InOrder(() =>
        {
            _backchannelLogoutNotifier.NotifyAsync(TestSid, _userId, _issuer, Arg.Any<CancellationToken>());
            _ssoClientSessionService.ForgetAsync(TestSid, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Logout_WithoutSid_DoesNotNotifyBackchannel()
    {
        SetupHttpContext(sid: null);

        await _controller.Logout();

        await _backchannelLogoutNotifier.DidNotReceive().NotifyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithSidAndParticipants_ReturnsTheNotificationPage()
    {
        SetupHttpContext(TestSid);
        _ssoClientSessionService
            .BuildLogoutNotificationUrisAsync(TestSid, _issuer, Arg.Any<CancellationToken>())
            .Returns(
            [
                new Uri("https://rp-one.example.com/bff/frontchannel-logout?iss=https%3A%2F%2Fid.example.com&sid=" + TestSid),
                new Uri("https://rp-two.example.com/bff/frontchannel-logout?iss=https%3A%2F%2Fid.example.com&sid=" + TestSid),
            ]);

        IActionResult result = await _controller.Logout();

        ContentResult page = result.Should().BeOfType<ContentResult>().Subject;
        page.ContentType.Should().StartWith("text/html");

        // Each participant's URI is loaded in a hidden iframe, HTML-attribute-encoded (the raw
        // query joiner & is not legal inside an attribute value).
        page.Content.Should().Contain("<iframe");
        page.Content.Should().Contain(
            "https://rp-one.example.com/bff/frontchannel-logout?iss=https%3A%2F%2Fid.example.com&amp;sid=" + TestSid);
        page.Content.Should().Contain(
            "https://rp-two.example.com/bff/frontchannel-logout?iss=https%3A%2F%2Fid.example.com&amp;sid=" + TestSid);

        // The page hands the browser back to this same endpoint with the completion marker so
        // phase two can run the OpenIddict end-session redirect.
        page.Content.Should().Contain("wallow_fc=done");
    }

    [Fact]
    public async Task Logout_WithSidAndParticipants_SignsOutCookieAndForgetsTheSession()
    {
        SetupHttpContext(TestSid);
        _ssoClientSessionService
            .BuildLogoutNotificationUrisAsync(TestSid, _issuer, Arg.Any<CancellationToken>())
            .Returns([new Uri("https://rp-one.example.com/bff/frontchannel-logout")]);

        await _controller.Logout();

        // The cookie dies in phase one — the notification page must already represent a
        // signed-out user — and the participation rows die with the session.
        await _authenticationService.Received(1).SignOutAsync(
            Arg.Any<HttpContext>(), IdentityConstants.ApplicationScheme, Arg.Any<AuthenticationProperties?>());
        await _ssoClientSessionService.Received(1).ForgetAsync(TestSid, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithSidButNoParticipants_SignsOutBothSchemesImmediately()
    {
        SetupHttpContext(TestSid);
        _ssoClientSessionService
            .BuildLogoutNotificationUrisAsync(TestSid, _issuer, Arg.Any<CancellationToken>())
            .Returns([]);

        IActionResult result = await _controller.Logout();

        result.Should().BeOfType<SignOutResult>();
        await _ssoClientSessionService.Received(1).ForgetAsync(TestSid, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithCompletionMarker_RunsTheEndSessionRedirectWithoutNotifyingAgain()
    {
        SetupHttpContext(TestSid, queryString: "?client_id=wallow-web&wallow_fc=done");

        IActionResult result = await _controller.Logout();

        SignOutResult signOut = result.Should().BeOfType<SignOutResult>().Subject;
        signOut.AuthenticationSchemes.Should()
            .ContainSingle(s => s == OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        await _ssoClientSessionService.DidNotReceive().BuildLogoutNotificationUrisAsync(
            Arg.Any<string>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>());

        // Phase one already revoked the session's tokens; the return trip must not walk again.
        await _accessRevoker.DidNotReceive().RevokeSessionAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Logout_WithInvalidRedirectUri_RedirectsToTheErrorPage()
    {
        SetupHttpContext(TestSid);
        OpenIddictServerTransaction transaction = new()
        {
            Request = new OpenIddictRequest { PostLogoutRedirectUri = "https://evil.example.com/phish" },
        };
        _controller.HttpContext.Features.Set(new OpenIddictServerAspNetCoreFeature { Transaction = transaction });
        _redirectUriValidator
            .IsAllowedAsync("https://evil.example.com/phish", Arg.Any<string?>())
            .Returns(Task.FromResult(false));

        IActionResult result = await _controller.Logout();

        RedirectResult redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be("https://auth.example.com/error?reason=invalid_redirect_uri");
        await _ssoClientSessionService.DidNotReceive().BuildLogoutNotificationUrisAsync(
            Arg.Any<string>(), Arg.Any<Uri>(), Arg.Any<CancellationToken>());
    }
}
