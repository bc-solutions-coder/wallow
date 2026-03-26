using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;
using Wallow.Auth.Models;
using Wallow.Auth.Services;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class ForgotPasswordTests : BunitContext
{
    private readonly IAuthApiClient _authApi;

    public ForgotPasswordTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        _authApi = Substitute.For<IAuthApiClient>();
        Services.AddSingleton(_authApi);
        Services.AddSingleton(new BrandingOptions { AppName = "TestApp" });
    }

    [Fact]
    public void Renders_FormWithEmailInputAndSubmitButton()
    {
        IRenderedComponent<ForgotPassword> cut = Render<ForgotPassword>();

        cut.Find("#forgot-email").Should().NotBeNull();
        cut.Markup.Should().Contain("Send reset link");
    }

    [Fact]
    public void Renders_BackToSignInLink()
    {
        IRenderedComponent<ForgotPassword> cut = Render<ForgotPassword>();

        cut.Find("a[href='/login']").TextContent.Should().Contain("Back to sign in");
    }

    [Fact]
    public async Task Submit_WithValidEmail_CallsApiAndShowsConfirmation()
    {
        _authApi.ForgotPasswordAsync(Arg.Any<ForgotPasswordRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(true));

        IRenderedComponent<ForgotPassword> cut = Render<ForgotPassword>();

        await cut.Find("#forgot-email").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "test@example.com" });

        AngleSharp.Dom.IElement button = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Send reset link"));
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Markup.Should().Contain("Check your email");
        await _authApi.Received(1).ForgotPasswordAsync(
            Arg.Is<ForgotPasswordRequest>(r => r.Email == "test@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithEmptyEmail_DoesNotCallApi()
    {
        IRenderedComponent<ForgotPassword> cut = Render<ForgotPassword>();

        AngleSharp.Dom.IElement button = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Send reset link"));
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _authApi.DidNotReceive().ForgotPasswordAsync(
            Arg.Any<ForgotPasswordRequest>(), Arg.Any<CancellationToken>());
    }
}
