using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Auth.Components.Layout;
using Wallow.Auth.Configuration;
using Wallow.Auth.Services;

namespace Wallow.Auth.Component.Tests.Layout;

public sealed class AuthLayoutTests : IDisposable
{
    private readonly BunitContext _ctx;
    private readonly IClientBrandingClient _brandingClient = Substitute.For<IClientBrandingClient>();

    public AuthLayoutTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.ComponentFactories.Add(new StubComponentFactory());
        _ctx.Services.AddSingleton(_brandingClient);
    }

    public void Dispose()
    {
        _ctx.Dispose();
    }

    [Fact]
    public void Render_WithBrandingOptions_DisplaysAppNameAndTagline()
    {
        BrandingOptions branding = new()
        {
            AppName = "TestApp",
            AppIcon = "test-icon.svg",
            Tagline = "Test tagline here"
        };
        _ctx.Services.AddSingleton(branding);

        IRenderedComponent<AuthLayout> cut = _ctx.Render<AuthLayout>(parameters =>
            parameters.Add(p => p.Body, "<p>Child content</p>"));

        string markup = cut.Markup;
        markup.Should().Contain("TestApp");
        markup.Should().Contain("test-icon.svg");
        markup.Should().Contain("Test tagline here");
        markup.Should().Contain("Child content");
    }

    [Fact]
    public void Render_WithDifferentBranding_ProducesDifferentOutput()
    {
        BrandingOptions branding = new()
        {
            AppName = "AnotherApp",
            AppIcon = "another-icon.png",
            Tagline = "Different tagline"
        };
        _ctx.Services.AddSingleton(branding);

        IRenderedComponent<AuthLayout> cut = _ctx.Render<AuthLayout>(parameters =>
            parameters.Add(p => p.Body, "<p>Body</p>"));

        string markup = cut.Markup;
        markup.Should().Contain("AnotherApp");
        markup.Should().Contain("another-icon.png");
        markup.Should().Contain("Different tagline");
        markup.Should().NotContain("TestApp");
    }

    [Fact]
    public void Render_WithRepositoryUrl_RendersLink()
    {
        BrandingOptions branding = new()
        {
            AppName = "LinkApp",
            AppIcon = "icon.svg",
            Tagline = "Has a repo",
            RepositoryUrl = "https://github.com/example/repo"
        };
        _ctx.Services.AddSingleton(branding);

        IRenderedComponent<AuthLayout> cut = _ctx.Render<AuthLayout>(parameters =>
            parameters.Add(p => p.Body, "<p>Body</p>"));

        string markup = cut.Markup;
        markup.Should().Contain("https://github.com/example/repo");
        markup.Should().Contain("href=\"https://github.com/example/repo\"");
    }

    [Fact]
    public void Render_WithoutRepositoryUrl_DoesNotRenderLink()
    {
        BrandingOptions branding = new()
        {
            AppName = "NoLinkApp",
            AppIcon = "icon.svg",
            Tagline = "No repo",
            RepositoryUrl = ""
        };
        _ctx.Services.AddSingleton(branding);

        IRenderedComponent<AuthLayout> cut = _ctx.Render<AuthLayout>(parameters =>
            parameters.Add(p => p.Body, "<p>Body</p>"));

        string markup = cut.Markup;
        markup.Should().NotContain("href=");
        markup.Should().Contain("NoLinkApp");
    }

    [Fact]
    public void Render_WithEmptyTagline_DoesNotRenderTaglineParagraph()
    {
        BrandingOptions branding = new()
        {
            AppName = "NoTagline",
            AppIcon = "icon.svg",
            Tagline = ""
        };
        _ctx.Services.AddSingleton(branding);

        IRenderedComponent<AuthLayout> cut = _ctx.Render<AuthLayout>(parameters =>
            parameters.Add(p => p.Body, "<p>Body</p>"));

        string markup = cut.Markup;
        markup.Should().Contain("NoTagline");
        markup.Should().NotContain("text-sm text-muted-foreground mt-1");
    }

    [Fact]
    public async Task Render_WithClientId_DisplaysClientBranding()
    {
        BrandingOptions branding = new()
        {
            AppName = "DefaultApp",
            AppIcon = "default-icon.svg",
            Tagline = "Default tagline"
        };
        _ctx.Services.AddSingleton(branding);

        ClientBrandingResponse clientBranding = new(
            ClientId: "client-123",
            DisplayName: "ClientApp",
            Tagline: "Client tagline",
            LogoUrl: "client-logo.png",
            ThemeJson: null);

        _brandingClient.GetBrandingAsync("client-123", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ClientBrandingResponse?>(clientBranding));

        BunitNavigationManager navMan = _ctx.Services.GetRequiredService<BunitNavigationManager>();
        navMan.NavigateTo("/?client_id=client-123");

        IRenderedComponent<AuthLayout> cut = _ctx.Render<AuthLayout>(parameters =>
            parameters.Add(p => p.Body, "<p>Body</p>"));

        await Task.Delay(50);
        cut.Render();

        string markup = cut.Markup;
        markup.Should().Contain("ClientApp");
        markup.Should().Contain("client-logo.png");
        markup.Should().Contain("Client tagline");
    }
}
