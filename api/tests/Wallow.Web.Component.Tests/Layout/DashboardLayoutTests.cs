using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Web.Components.Layout;
using Wallow.Web.Components.Shared;
using Wallow.Web.Configuration;

namespace Wallow.Web.Component.Tests.Layout;

public sealed class DashboardLayoutTests : BunitContext
{
    public DashboardLayoutTests()
    {
        Services.AddSingleton(new BrandingOptions());
        ComponentFactories.AddStub<BlazorReadyIndicator>();
        BunitAuthorizationContext authContext = AddAuthorization();
        authContext.SetAuthorized("testuser");
        authContext.SetRoles("admin");
    }

    [Fact]
    public void Render_ShowsSidebarNavLinks()
    {
        RenderFragment body = builder => builder.AddContent(0, "Dashboard Content");
        IRenderedComponent<DashboardLayout> cut = Render<DashboardLayout>(p => p.Add(x => x.Body, body));

        cut.Markup.Should().Contain("My Apps");
        cut.Markup.Should().Contain("Inquiries");
        cut.Markup.Should().Contain("Settings");
        cut.Markup.Should().Contain("Sign Out");
    }

    [Fact]
    public void Render_AdminUser_ShowsOrganizationsLink()
    {
        RenderFragment body = builder => builder.AddContent(0, "Dashboard Content");
        IRenderedComponent<DashboardLayout> cut = Render<DashboardLayout>(p => p.Add(x => x.Body, body));

        cut.Markup.Should().Contain("Organizations");
    }

    [Fact]
    public void Render_ShowsBrandingName()
    {
        RenderFragment body = builder => builder.AddContent(0, "Test");
        IRenderedComponent<DashboardLayout> cut = Render<DashboardLayout>(p => p.Add(x => x.Body, body));

        cut.Markup.Should().Contain("Wallow");
    }

    [Fact]
    public void Render_ShowsBodyContent()
    {
        RenderFragment body = builder => builder.AddContent(0, "My Custom Dashboard Content");
        IRenderedComponent<DashboardLayout> cut = Render<DashboardLayout>(p => p.Add(x => x.Body, body));

        cut.Markup.Should().Contain("My Custom Dashboard Content");
    }
}

public sealed class DashboardLayoutNonAdminTests : BunitContext
{
    public DashboardLayoutNonAdminTests()
    {
        Services.AddSingleton(new BrandingOptions());
        ComponentFactories.AddStub<BlazorReadyIndicator>();
        BunitAuthorizationContext authContext = AddAuthorization();
        authContext.SetAuthorized("testuser");
    }

    [Fact]
    public void Render_NonAdminUser_DoesNotShowOrganizationsLink()
    {
        RenderFragment body = builder => builder.AddContent(0, "Content");
        IRenderedComponent<DashboardLayout> cut = Render<DashboardLayout>(p => p.Add(x => x.Body, body));

        cut.Markup.Should().NotContain("Organizations");
    }
}
