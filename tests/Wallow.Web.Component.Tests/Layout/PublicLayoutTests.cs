using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Web.Components.Layout;
using Wallow.Web.Configuration;

namespace Wallow.Web.Component.Tests.Layout;

public sealed class PublicLayoutTests : BunitContext
{
    public PublicLayoutTests()
    {
        Services.AddSingleton(new BrandingOptions());
    }

    [Fact]
    public void Render_ShowsNavbarAndFooter()
    {
        RenderFragment body = builder => builder.AddContent(0, "Page Content");
        IRenderedComponent<PublicLayout> cut = Render<PublicLayout>(p => p.Add(x => x.Body, body));

        cut.Markup.Should().Contain("Wallow");
        cut.Markup.Should().Contain("Features");
        cut.Markup.Should().Contain("Docs");
        cut.Markup.Should().Contain("GitHub");
        cut.Markup.Should().Contain("Get Started");
        cut.Markup.Should().Contain("MIT Licensed");
    }

    [Fact]
    public void Render_ShowsBodyContent()
    {
        RenderFragment body = builder => builder.AddContent(0, "My Public Page Content");
        IRenderedComponent<PublicLayout> cut = Render<PublicLayout>(p => p.Add(x => x.Body, body));

        cut.Markup.Should().Contain("My Public Page Content");
    }
}
