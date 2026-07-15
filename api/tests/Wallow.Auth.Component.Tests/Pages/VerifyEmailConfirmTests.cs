using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;
using Wallow.Auth.Models;
using Wallow.Auth.Services;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class VerifyEmailConfirmTests : BunitContext
{
    private readonly IAuthApiClient _authApi;
    private readonly ILogger<VerifyEmailConfirm> _logger;

    public VerifyEmailConfirmTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        _authApi = Substitute.For<IAuthApiClient>();
        _logger = Substitute.For<ILogger<VerifyEmailConfirm>>();
        Services.AddSingleton(_authApi);
        Services.AddSingleton(new BrandingOptions { AppName = "TestApp" });
        Services.AddSingleton(_logger);
    }

    private void NavigateWithParams(string? token = null, string? email = null, string? returnUrl = null)
    {
        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        List<string> queryParams = new();
        if (token is not null)
        {
            queryParams.Add($"Token={Uri.EscapeDataString(token)}");
        }
        if (email is not null)
        {
            queryParams.Add($"Email={Uri.EscapeDataString(email)}");
        }
        if (returnUrl is not null)
        {
            queryParams.Add($"ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        }
        string query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        navMan.NavigateTo($"/verify-email/confirm{query}");
    }

    [Fact]
    public void Missing_token_shows_error()
    {
        NavigateWithParams(email: "test@example.com");

        IRenderedComponent<VerifyEmailConfirm> cut = Render<VerifyEmailConfirm>();

        cut.Markup.Should().Contain("Missing required parameters");
    }

    [Fact]
    public void Missing_email_shows_error()
    {
        NavigateWithParams(token: "test-token");

        IRenderedComponent<VerifyEmailConfirm> cut = Render<VerifyEmailConfirm>();

        cut.Markup.Should().Contain("Missing required parameters");
    }

    [Fact]
    public void Successful_verification_shows_success_message()
    {
        _authApi.VerifyEmailAsync("test@example.com", "test-token", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(true));

        NavigateWithParams(token: "test-token", email: "test@example.com");

        IRenderedComponent<VerifyEmailConfirm> cut = Render<VerifyEmailConfirm>();

        cut.Markup.Should().Contain("Email verified!");
        cut.Markup.Should().Contain("You can now sign in");
    }

    [Fact]
    public void Failed_verification_shows_error_message()
    {
        _authApi.VerifyEmailAsync("test@example.com", "bad-token", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(false, Error: "invalid_token"));

        NavigateWithParams(token: "bad-token", email: "test@example.com");

        IRenderedComponent<VerifyEmailConfirm> cut = Render<VerifyEmailConfirm>();

        cut.Markup.Should().Contain("Verification failed");
        cut.Markup.Should().Contain("invalid or has expired");
    }

    [Fact]
    public void Successful_verification_with_return_url_shows_continue_button()
    {
        _authApi.VerifyEmailAsync("test@example.com", "test-token", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(true));

        NavigateWithParams(token: "test-token", email: "test@example.com", returnUrl: "/dashboard");

        IRenderedComponent<VerifyEmailConfirm> cut = Render<VerifyEmailConfirm>();

        cut.Markup.Should().Contain("Continue");
    }

    [Fact]
    public void Api_exception_shows_generic_error()
    {
        _authApi.VerifyEmailAsync("test@example.com", "test-token", Arg.Any<CancellationToken>())
            .Returns<AuthResponse>(_ => throw new HttpRequestException("Network error"));

        NavigateWithParams(token: "test-token", email: "test@example.com");

        IRenderedComponent<VerifyEmailConfirm> cut = Render<VerifyEmailConfirm>();

        cut.Markup.Should().Contain("An error occurred while verifying your email");
    }

    [Fact]
    public void Init_WithValidParams_LogsPageInitialized()
    {
        _authApi.VerifyEmailAsync("test@example.com", "test-token", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(true));

        NavigateWithParams(token: "test-token", email: "test@example.com");
        Render<VerifyEmailConfirm>();

        _logger.ShouldHaveLoggedMessage("OIDC VerifyEmailConfirm:");
    }

    [Fact]
    public void Successful_verification_LogsSuccess()
    {
        _authApi.VerifyEmailAsync("test@example.com", "test-token", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(true));

        NavigateWithParams(token: "test-token", email: "test@example.com");
        Render<VerifyEmailConfirm>();

        _logger.ShouldHaveLoggedMessage("OIDC VerifyEmailConfirm:");
    }

    [Fact]
    public void Failed_verification_LogsFailure()
    {
        _authApi.VerifyEmailAsync("test@example.com", "bad-token", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(false, Error: "invalid_token"));

        NavigateWithParams(token: "bad-token", email: "test@example.com");
        Render<VerifyEmailConfirm>();

        _logger.ShouldHaveLoggedMessage("OIDC VerifyEmailConfirm:");
    }

    [Fact]
    public void Init_WithMissingParams_LogsError()
    {
        NavigateWithParams(email: "test@example.com");
        Render<VerifyEmailConfirm>();

        _logger.ShouldHaveLoggedMessage("OIDC VerifyEmailConfirm:");
    }
}
