using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Web.Components.Pages.Dashboard;
using Wallow.Web.Components.Shared;
using Wallow.Web.Configuration;
using Wallow.Web.Services;

namespace Wallow.Web.Component.Tests.Pages.Dashboard;

public sealed class SettingsTests : BunitContext
{
    public SettingsTests()
    {
        Services.AddSingleton(new BrandingOptions());
        Services.AddSingleton(Substitute.For<IMfaApiClient>());
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        ComponentFactories.AddStub<BlazorReadyIndicator>();
    }

    [Fact]
    public void Render_WithAuthenticatedUser_ShowsProfile()
    {
        BunitAuthorizationContext authContext = AddAuthorization();
        authContext.SetAuthorized("testuser");
        authContext.SetClaims(
            new Claim("name", "Jane Doe"),
            new Claim("email", "jane@example.com"),
            new Claim(ClaimTypes.Role, "admin"));

        IRenderedComponent<Settings> cut = Render<Settings>();

        cut.Markup.Should().Contain("Settings");
        cut.Markup.Should().Contain("Jane Doe");
        cut.Markup.Should().Contain("jane@example.com");
        cut.Markup.Should().Contain("admin");
    }

    [Fact]
    public void Render_WithNoRoles_ShowsNoRolesMessage()
    {
        BunitAuthorizationContext authContext = AddAuthorization();
        authContext.SetAuthorized("testuser");
        authContext.SetClaims(
            new Claim("name", "Jane Doe"),
            new Claim("email", "jane@example.com"));

        IRenderedComponent<Settings> cut = Render<Settings>();

        cut.Markup.Should().Contain("No roles assigned");
    }
}
