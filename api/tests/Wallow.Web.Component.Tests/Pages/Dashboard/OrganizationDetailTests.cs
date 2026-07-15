using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Web.Components.Pages.Dashboard;
using Wallow.Web.Configuration;
using Wallow.Web.Models;
using Wallow.Web.Services;

namespace Wallow.Web.Component.Tests.Pages.Dashboard;

public sealed class OrganizationDetailTests : BunitContext
{
    private readonly IOrganizationApiService _orgService;
    private readonly IAppRegistrationService _appService;

    public OrganizationDetailTests()
    {
        _orgService = Substitute.For<IOrganizationApiService>();
        _appService = Substitute.For<IAppRegistrationService>();
        Services.AddSingleton(_orgService);
        Services.AddSingleton(_appService);
        Services.AddSingleton(new BrandingOptions());
        BunitAuthorizationContext authContext = AddAuthorization();
        authContext.SetAuthorized("testuser");
    }

    [Fact]
    public void Render_WithOrgAndMembers_ShowsDetails()
    {
        Guid orgId = Guid.NewGuid();
        OrganizationModel org = new(orgId, "Acme Corp", "acme.com", 2);
        List<OrganizationMemberModel> members =
        [
            new(Guid.NewGuid(), "alice@acme.com", "Alice", "Smith", true, ["admin"]),
            new(Guid.NewGuid(), "bob@acme.com", "Bob", "Jones", true, ["member"])
        ];
        List<ClientModel> clients = [];

        _orgService.GetOrganizationAsync(orgId, Arg.Any<CancellationToken>()).Returns(org);
        _orgService.GetMembersAsync(orgId, Arg.Any<CancellationToken>()).Returns(members);
        _orgService.GetClientsByTenantAsync(orgId, Arg.Any<CancellationToken>()).Returns(clients);

        IRenderedComponent<OrganizationDetail> cut = Render<OrganizationDetail>(p => p.Add(x => x.OrgId, orgId));

        cut.Markup.Should().Contain("Acme Corp");
        cut.Markup.Should().Contain("alice@acme.com");
        cut.Markup.Should().Contain("bob@acme.com");
        cut.Markup.Should().Contain("admin");
        cut.Markup.Should().Contain("member");
    }

    [Fact]
    public void Render_WithNullOrg_ShowsNotFound()
    {
        Guid orgId = Guid.NewGuid();
        _orgService.GetOrganizationAsync(orgId, Arg.Any<CancellationToken>()).Returns((OrganizationModel?)null);
        _orgService.GetMembersAsync(orgId, Arg.Any<CancellationToken>()).Returns(new List<OrganizationMemberModel>());
        _orgService.GetClientsByTenantAsync(orgId, Arg.Any<CancellationToken>()).Returns(new List<ClientModel>());

        IRenderedComponent<OrganizationDetail> cut = Render<OrganizationDetail>(p => p.Add(x => x.OrgId, orgId));

        cut.Markup.Should().Contain("Organization not found");
    }

    [Fact]
    public void Render_WithClients_ShowsClientTable()
    {
        Guid orgId = Guid.NewGuid();
        OrganizationModel org = new(orgId, "Acme Corp", "acme.com", 1);
        List<OrganizationMemberModel> members = [];
        List<ClientModel> clients =
        [
            new()
            {
                Id = "1",
                Name = "Web Client",
                ClientId = "web-client-id",
                RedirectUris = ["https://localhost"],
                PostLogoutRedirectUris = []
            }
        ];

        _orgService.GetOrganizationAsync(orgId, Arg.Any<CancellationToken>()).Returns(org);
        _orgService.GetMembersAsync(orgId, Arg.Any<CancellationToken>()).Returns(members);
        _orgService.GetClientsByTenantAsync(orgId, Arg.Any<CancellationToken>()).Returns(clients);

        IRenderedComponent<OrganizationDetail> cut = Render<OrganizationDetail>(p => p.Add(x => x.OrgId, orgId));

        cut.Markup.Should().Contain("Web Client");
        cut.Markup.Should().Contain("web-client-id");
    }
}
