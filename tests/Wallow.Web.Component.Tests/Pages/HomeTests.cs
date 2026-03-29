using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Web.Components.Pages;
using Wallow.Web.Configuration;

namespace Wallow.Web.Component.Tests.Pages;

public sealed class HomeTests : BunitContext
{
    public HomeTests()
    {
        Services.AddSingleton(new BrandingOptions());
    }

    [Fact]
    public void Render_Unauthenticated_ShowsLandingPage()
    {
        BunitAuthorizationContext authContext = AddAuthorization();
        authContext.SetNotAuthorized();

        IRenderedComponent<Home> cut = Render<Home>();

        cut.Markup.Should().Contain("Wallow in it");
        cut.Markup.Should().Contain("Get Started");
        cut.Markup.Should().Contain(".NET 10");
    }

    [Fact]
    public void Render_Authenticated_RedirectsToDashboard()
    {
        BunitAuthorizationContext authContext = AddAuthorization();
        authContext.SetAuthorized("testuser");

        Render<Home>();

        BunitNavigationManager nav = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        nav.History.Should().Contain(h => h.Uri.Contains("/dashboard/apps"));
    }
}

public sealed class HomeLandingPageDisabledTests : BunitContext
{
    public HomeLandingPageDisabledTests()
    {
        Services.AddSingleton(new BrandingOptions { LandingPage = new LandingPageOptions { Enabled = false } });
    }

    [Fact]
    public void Render_LandingPageDisabled_RedirectsToLogin()
    {
        BunitAuthorizationContext authContext = AddAuthorization();
        authContext.SetNotAuthorized();

        Render<Home>();

        BunitNavigationManager nav = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        nav.History.Should().Contain(h => h.Uri.Contains("/authentication/login"));
    }
}
