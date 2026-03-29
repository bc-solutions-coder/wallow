using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Web.Components.Pages.Dashboard;
using Wallow.Web.Configuration;
using Wallow.Web.Models;
using Wallow.Web.Services;

namespace Wallow.Web.Component.Tests.Pages.Dashboard;

public sealed class OrganizationsTests : BunitContext
{
    private readonly IOrganizationApiService _orgService;

    public OrganizationsTests()
    {
        _orgService = Substitute.For<IOrganizationApiService>();
        Services.AddSingleton(_orgService);
        Services.AddSingleton(new BrandingOptions());
        BunitAuthorizationContext authContext = AddAuthorization();
        authContext.SetAuthorized("testuser");
    }

    [Fact]
    public void Render_WithOrganizations_ShowsOrgNames()
    {
        List<OrganizationModel> orgs =
        [
            new(Guid.NewGuid(), "Acme Corp", "acme.com", 5),
            new(Guid.NewGuid(), "Globex Inc", null, 12)
        ];
        _orgService.GetOrganizationsAsync(Arg.Any<CancellationToken>()).Returns(orgs);

        IRenderedComponent<Organizations> cut = Render<Organizations>();

        cut.Markup.Should().Contain("Acme Corp");
        cut.Markup.Should().Contain("Globex Inc");
        cut.Markup.Should().Contain("acme.com");
    }

    [Fact]
    public void Render_WithEmptyList_ShowsEmptyState()
    {
        _orgService.GetOrganizationsAsync(Arg.Any<CancellationToken>()).Returns(new List<OrganizationModel>());

        IRenderedComponent<Organizations> cut = Render<Organizations>();

        cut.Markup.Should().Contain("No organizations yet");
        cut.Markup.Should().Contain("Create your first organization");
    }

    [Fact]
    public void Render_WithNullList_ShowsEmptyState()
    {
        _orgService.GetOrganizationsAsync(Arg.Any<CancellationToken>()).Returns((List<OrganizationModel>)null!);

        IRenderedComponent<Organizations> cut = Render<Organizations>();

        cut.Markup.Should().Contain("No organizations yet");
    }
}
