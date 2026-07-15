using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class VerifyEmailTests : BunitContext
{
    public VerifyEmailTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        Services.AddSingleton(new BrandingOptions { AppName = "TestApp" });
    }

    [Fact]
    public void Renders_pending_verification_message()
    {
        IRenderedComponent<VerifyEmail> cut = Render<VerifyEmail>();

        cut.Markup.Should().Contain("Check your email");
        cut.Markup.Should().Contain("verification link");
        cut.Markup.Should().Contain("check your spam folder");
    }

    [Fact]
    public void Back_to_sign_in_link_without_return_url_points_to_login()
    {
        IRenderedComponent<VerifyEmail> cut = Render<VerifyEmail>();

        cut.Find("a").GetAttribute("href").Should().Be("/login");
    }

    [Fact]
    public void Back_to_sign_in_link_with_return_url_includes_encoded_return_url()
    {
        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.NavigateTo("/verify-email?ReturnUrl=%2Fdashboard");

        IRenderedComponent<VerifyEmail> cut = Render<VerifyEmail>();

        cut.Find("a").GetAttribute("href").Should().Contain("/login?returnUrl=");
    }
}
