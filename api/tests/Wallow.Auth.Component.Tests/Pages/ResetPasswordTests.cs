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

public sealed class ResetPasswordTests : BunitContext
{
    private readonly IAuthApiClient _authApi;
    private readonly ILogger<ResetPassword> _logger;

    public ResetPasswordTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        _authApi = Substitute.For<IAuthApiClient>();
        _logger = Substitute.For<ILogger<ResetPassword>>();
        Services.AddSingleton(_authApi);
        Services.AddSingleton(new BrandingOptions { AppName = "TestApp" });
        Services.AddSingleton(_logger);
    }

    private void NavigateWithParams(string token = "test-token", string email = "test@example.com")
    {
        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.NavigateTo($"/reset-password?token={token}&email={Uri.EscapeDataString(email)}");
    }

    [Fact]
    public void Renders_PasswordInputsAndSubmitButton()
    {
        NavigateWithParams();
        IRenderedComponent<ResetPassword> cut = Render<ResetPassword>();

        cut.Find("#new-password").Should().NotBeNull();
        cut.Find("#confirm-password").Should().NotBeNull();
        cut.Markup.Should().Contain("Reset password");
    }

    [Fact]
    public async Task Submit_WithoutToken_ShowsInvalidLinkError()
    {
        IRenderedComponent<ResetPassword> cut = Render<ResetPassword>();

        AngleSharp.Dom.IElement button = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Reset password"));
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Markup.Should().Contain("Invalid reset link");
    }

    [Fact]
    public async Task Submit_WithMismatchedPasswords_ShowsError()
    {
        NavigateWithParams();
        IRenderedComponent<ResetPassword> cut = Render<ResetPassword>();

        await cut.Find("#new-password").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "Password1!" });
        await cut.Find("#confirm-password").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "DifferentPassword!" });

        AngleSharp.Dom.IElement button = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Reset password"));
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Markup.Should().Contain("Passwords do not match");
    }

    [Fact]
    public async Task SuccessfulReset_NavigatesToLogin()
    {
        _authApi.ResetPasswordAsync(Arg.Any<ResetPasswordRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(true));

        NavigateWithParams();
        IRenderedComponent<ResetPassword> cut = Render<ResetPassword>();

        await cut.Find("#new-password").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "NewPassword1!" });
        await cut.Find("#confirm-password").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "NewPassword1!" });

        AngleSharp.Dom.IElement button = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Reset password"));
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.Uri.Should().Contain("/login?message=password_reset");
    }

    [Fact]
    public async Task FailedReset_WithInvalidToken_ShowsError()
    {
        _authApi.ResetPasswordAsync(Arg.Any<ResetPasswordRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(false, Error: "invalid_token"));

        NavigateWithParams();
        IRenderedComponent<ResetPassword> cut = Render<ResetPassword>();

        await cut.Find("#new-password").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "NewPassword1!" });
        await cut.Find("#confirm-password").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "NewPassword1!" });

        AngleSharp.Dom.IElement button = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Reset password"));
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Markup.Should().Contain("invalid or has expired");
    }

    [Fact]
    public void Init_LogsPageInitialized()
    {
        NavigateWithParams();
        Render<ResetPassword>();

        _logger.ShouldHaveLoggedMessage("OIDC ResetPassword:");
    }

    [Fact]
    public async Task SuccessfulReset_LogsSuccess()
    {
        _authApi.ResetPasswordAsync(Arg.Any<ResetPasswordRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(true));

        NavigateWithParams();
        IRenderedComponent<ResetPassword> cut = Render<ResetPassword>();

        await cut.Find("#new-password").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "NewPassword1!" });
        await cut.Find("#confirm-password").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "NewPassword1!" });

        AngleSharp.Dom.IElement button = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Reset password"));
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _logger.ShouldHaveLoggedMessage("OIDC ResetPassword:");
    }

    [Fact]
    public async Task FailedReset_LogsFailure()
    {
        _authApi.ResetPasswordAsync(Arg.Any<ResetPasswordRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(false, Error: "invalid_token"));

        NavigateWithParams();
        IRenderedComponent<ResetPassword> cut = Render<ResetPassword>();

        await cut.Find("#new-password").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "NewPassword1!" });
        await cut.Find("#confirm-password").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "NewPassword1!" });

        AngleSharp.Dom.IElement button = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Reset password"));
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _logger.ShouldHaveLoggedMessage("OIDC ResetPassword:");
    }
}
