using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;
using Wallow.Auth.Models;
using Wallow.Auth.Services;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class LoginTests : BunitContext
{
    private readonly IAuthApiClient _authClient;

    public LoginTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());

        _authClient = Substitute.For<IAuthApiClient>();
        _authClient.GetExternalProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        Services.AddSingleton(_authClient);
        Services.AddSingleton(new BrandingOptions());
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
    }

    [Fact]
    public void Renders_EmailAndPasswordFields()
    {
        IRenderedComponent<Login> cut = Render<Login>();

        AngleSharp.Dom.IElement emailInput = cut.Find("input[placeholder='name@example.com']");
        AngleSharp.Dom.IElement passwordInput = cut.Find("input[placeholder='Enter your password']");

        emailInput.Should().NotBeNull();
        passwordInput.Should().NotBeNull();
    }

    [Fact]
    public void Renders_SignInTitle()
    {
        IRenderedComponent<Login> cut = Render<Login>();

        cut.Markup.Should().Contain("Sign in to your account");
    }

    [Fact]
    public void Submit_WithValidCredentials_CallsLoginAndShowsSuccess()
    {
        _authClient.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: true));

        IRenderedComponent<Login> cut = Render<Login>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");
        cut.Find("input[placeholder='Enter your password']").Input("P@ssword1");
        cut.Find("form").Submit();

        _authClient.Received(1).LoginAsync(
            Arg.Is<LoginRequest>(r => r.Email == "user@test.com" && r.Password == "P@ssword1"),
            Arg.Any<CancellationToken>());

        cut.Markup.Should().Contain("You are now signed in.");
    }

    [Fact]
    public void Submit_WithInvalidCredentials_DisplaysError()
    {
        _authClient.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: false, Error: "invalid_credentials"));

        IRenderedComponent<Login> cut = Render<Login>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");
        cut.Find("input[placeholder='Enter your password']").Input("wrong");
        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Invalid email or password.");
    }

    [Fact]
    public void Submit_WithEmptyFields_DisplaysValidationError()
    {
        IRenderedComponent<Login> cut = Render<Login>();

        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Please enter your email and password.");
        _authClient.DidNotReceive().LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Submit_WithReturnUrl_NavigatesToReturnUrl()
    {
        _authClient.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: true));

        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.NavigateTo("/login?ReturnUrl=%2Fdashboard");

        IRenderedComponent<Login> cut = Render<Login>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");
        cut.Find("input[placeholder='Enter your password']").Input("P@ssword1");
        cut.Find("form").Submit();

        navMan.Uri.Should().Contain("/dashboard");
    }

    [Fact]
    public void Submit_WithAbsoluteReturnUrl_NavigatesToError()
    {
        _authClient.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: true));

        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.NavigateTo("/login?ReturnUrl=https%3A%2F%2Fevil.com");

        IRenderedComponent<Login> cut = Render<Login>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");
        cut.Find("input[placeholder='Enter your password']").Input("P@ssword1");
        cut.Find("form").Submit();

        navMan.Uri.Should().Contain("/error?reason=invalid_redirect_uri");
    }

    [Fact]
    public void ErrorQueryParam_DisplaysErrorMessage()
    {
        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.NavigateTo("/login?Error=session_expired");

        IRenderedComponent<Login> cut = Render<Login>();

        cut.Markup.Should().Contain("Your session has expired. Please try again.");
    }

    [Fact]
    public void Submit_WhenServerUnreachable_DisplaysConnectionError()
    {
        _authClient.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns<AuthResponse>(_ => throw new HttpRequestException("Connection refused"));

        IRenderedComponent<Login> cut = Render<Login>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");
        cut.Find("input[placeholder='Enter your password']").Input("P@ssword1");
        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Unable to reach the server.");
    }

    [Fact]
    public void Renders_RegisterLink()
    {
        IRenderedComponent<Login> cut = Render<Login>();

        cut.Markup.Should().Contain("have an account?");
        cut.Find("a[href='/register']").Should().NotBeNull();
    }

    [Fact]
    public void Submit_WithLockedOutAccount_DisplaysLockedOutError()
    {
        _authClient.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: false, Error: "locked_out"));

        IRenderedComponent<Login> cut = Render<Login>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");
        cut.Find("input[placeholder='Enter your password']").Input("P@ssword1");
        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Account locked. Try again later.");
    }

    [Fact]
    public void Submit_WithUnconfirmedEmail_DisplaysVerificationError()
    {
        _authClient.LoginAsync(Arg.Any<LoginRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: false, Error: "email_not_confirmed"));

        IRenderedComponent<Login> cut = Render<Login>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");
        cut.Find("input[placeholder='Enter your password']").Input("P@ssword1");
        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Please verify your email before signing in.");
    }
}
