using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Web.Components.Pages.Dashboard;
using Wallow.Web.Configuration;
using Wallow.Web.Models;
using Wallow.Web.Services;

namespace Wallow.Web.Component.Tests.Pages.Dashboard;

public sealed class AppsTests : BunitContext
{
    private readonly IAppRegistrationService _appService;

    public AppsTests()
    {
        _appService = Substitute.For<IAppRegistrationService>();
        Services.AddSingleton(_appService);
        Services.AddSingleton(new BrandingOptions());
        BunitAuthorizationContext authContext = AddAuthorization();
        authContext.SetAuthorized("testuser");
    }

    [Fact]
    public void Render_WithApps_ShowsAppNames()
    {
        List<AppModel> apps =
        [
            new("client-1", "My First App", "public", ["https://localhost"], DateTimeOffset.UtcNow),
            new("client-2", "My Second App", "confidential", ["https://example.com"], DateTimeOffset.UtcNow)
        ];
        _appService.GetAppsAsync(Arg.Any<CancellationToken>()).Returns(apps);

        IRenderedComponent<Apps> cut = Render<Apps>();

        cut.Markup.Should().Contain("My First App");
        cut.Markup.Should().Contain("My Second App");
        cut.Markup.Should().Contain("client-1");
        cut.Markup.Should().Contain("client-2");
    }

    [Fact]
    public void Render_WithEmptyList_ShowsEmptyState()
    {
        _appService.GetAppsAsync(Arg.Any<CancellationToken>()).Returns(new List<AppModel>());

        IRenderedComponent<Apps> cut = Render<Apps>();

        cut.Markup.Should().Contain("No apps yet");
        cut.Markup.Should().Contain("Register your first app");
    }

    [Fact]
    public void Render_WithNullList_ShowsEmptyState()
    {
        _appService.GetAppsAsync(Arg.Any<CancellationToken>()).Returns((List<AppModel>?)null!);

        IRenderedComponent<Apps> cut = Render<Apps>();

        cut.Markup.Should().Contain("No apps yet");
    }
}
