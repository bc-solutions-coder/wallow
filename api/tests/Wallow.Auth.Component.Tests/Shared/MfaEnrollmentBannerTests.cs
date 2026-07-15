using Bunit;
using Wallow.Auth.Components.Shared;

namespace Wallow.Auth.Component.Tests.Shared;

public sealed class MfaEnrollmentBannerTests : IDisposable
{
    private readonly BunitContext _ctx;

    public MfaEnrollmentBannerTests()
    {
        _ctx = new BunitContext();
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.ComponentFactories.Add(new StubComponentFactory());
    }

    public void Dispose()
    {
        _ctx.Dispose();
    }

    [Fact]
    public void Render_WhenVisibleWithGraceDeadline_ShowsBannerWithDeadline()
    {
        DateTime deadline = new(2026, 4, 15);

        IRenderedComponent<MfaEnrollmentBanner> cut = _ctx.Render<MfaEnrollmentBanner>(parameters =>
            parameters
                .Add(p => p.Visible, true)
                .Add(p => p.GraceDeadline, deadline));

        string markup = cut.Markup;
        markup.Should().Contain("MFA enrollment required");
        markup.Should().Contain("April 15, 2026");
        markup.Should().Contain("Set up now");
    }

    [Fact]
    public void Render_WhenVisibleWithoutGraceDeadline_ShowsGenericMessage()
    {
        IRenderedComponent<MfaEnrollmentBanner> cut = _ctx.Render<MfaEnrollmentBanner>(parameters =>
            parameters
                .Add(p => p.Visible, true)
                .Add(p => p.GraceDeadline, null));

        string markup = cut.Markup;
        markup.Should().Contain("MFA enrollment required");
        markup.Should().Contain("as soon as possible");
        markup.Should().NotContain("before");
    }

    [Fact]
    public void Render_WhenNotVisible_DoesNotRenderBanner()
    {
        IRenderedComponent<MfaEnrollmentBanner> cut = _ctx.Render<MfaEnrollmentBanner>(parameters =>
            parameters
                .Add(p => p.Visible, false)
                .Add(p => p.GraceDeadline, new DateTime(2026, 5, 1)));

        string markup = cut.Markup;
        markup.Should().BeEmpty();
    }

    [Fact]
    public void Render_WhenDismissed_HidesBanner()
    {
        IRenderedComponent<MfaEnrollmentBanner> cut = _ctx.Render<MfaEnrollmentBanner>(parameters =>
            parameters
                .Add(p => p.Visible, true)
                .Add(p => p.GraceDeadline, new DateTime(2026, 4, 15)));

        cut.Find("button[type='button']").Click();

        string markup = cut.Markup;
        markup.Should().BeEmpty();
    }

    [Fact]
    public void Render_WhenVisible_ContainsEnrollLink()
    {
        IRenderedComponent<MfaEnrollmentBanner> cut = _ctx.Render<MfaEnrollmentBanner>(parameters =>
            parameters
                .Add(p => p.Visible, true)
                .Add(p => p.GraceDeadline, null));

        string markup = cut.Markup;
        markup.Should().Contain("/mfa/enroll");
    }
}
