using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Web.Components.Pages.Dashboard;
using Wallow.Web.Configuration;
using Wallow.Web.Models;
using Wallow.Web.Services;

namespace Wallow.Web.Component.Tests.Pages.Dashboard;

public sealed class InquiriesTests : BunitContext
{
    private readonly IInquiryService _inquiryService;

    public InquiriesTests()
    {
        _inquiryService = Substitute.For<IInquiryService>();
        Services.AddSingleton(_inquiryService);
        Services.AddSingleton(new BrandingOptions());
        BunitAuthorizationContext authContext = AddAuthorization();
        authContext.SetAuthorized("testuser");
        authContext.SetClaims(
            new Claim("name", "Test User"),
            new Claim("email", "test@example.com"));
    }

    [Fact]
    public void Render_ShowsInquiryForm()
    {
        IRenderedComponent<Inquiries> cut = Render<Inquiries>();

        cut.Markup.Should().Contain("Submit an Inquiry");
        cut.Markup.Should().Contain("Name");
        cut.Markup.Should().Contain("Email");
        cut.Markup.Should().Contain("Message");
        cut.Markup.Should().Contain("Submit Inquiry");
    }

    [Fact]
    public void Render_PrefillsNameAndEmailFromClaims()
    {
        IRenderedComponent<Inquiries> cut = Render<Inquiries>();

        IReadOnlyList<AngleSharp.Dom.IElement> inputs = cut.FindAll("input");
        bool hasTestUser = inputs.Any(i => i.GetAttribute("value") == "Test User");
        bool hasTestEmail = inputs.Any(i => i.GetAttribute("value") == "test@example.com");

        hasTestUser.Should().BeTrue();
        hasTestEmail.Should().BeTrue();
    }

    [Fact]
    public void Render_ShowsProjectTypeOptions()
    {
        IRenderedComponent<Inquiries> cut = Render<Inquiries>();

        cut.Markup.Should().Contain("Web Application");
        cut.Markup.Should().Contain("Mobile Application");
        cut.Markup.Should().Contain("SaaS Platform");
    }

    [Fact]
    public async Task Submit_WhenSuccessful_ShowsConfirmation()
    {
        _inquiryService.SubmitInquiryAsync(Arg.Any<InquiryModel>(), Arg.Any<CancellationToken>())
            .Returns(true);

        IRenderedComponent<Inquiries> cut = Render<Inquiries>();

        AngleSharp.Dom.IElement form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Inquiry Submitted");
            cut.Markup.Should().Contain("Submit Another");
        });
    }

    [Fact]
    public async Task Submit_WhenFails_ShowsErrorMessage()
    {
        _inquiryService.SubmitInquiryAsync(Arg.Any<InquiryModel>(), Arg.Any<CancellationToken>())
            .Returns(false);

        IRenderedComponent<Inquiries> cut = Render<Inquiries>();

        AngleSharp.Dom.IElement form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Failed to submit inquiry. Please try again.");
        });
    }

    [Fact]
    public async Task SubmitAnother_ResetsForm()
    {
        _inquiryService.SubmitInquiryAsync(Arg.Any<InquiryModel>(), Arg.Any<CancellationToken>())
            .Returns(true);

        IRenderedComponent<Inquiries> cut = Render<Inquiries>();

        AngleSharp.Dom.IElement form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Submit Another"));

        AngleSharp.Dom.IElement submitAnotherButton = cut.Find("button");
        await submitAnotherButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Submit an Inquiry");
            cut.Markup.Should().Contain("Submit Inquiry");
        });
    }
}
