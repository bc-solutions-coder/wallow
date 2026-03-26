using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;
using Wallow.Auth.Models;
using Wallow.Auth.Services;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class RegisterTests : BunitContext
{
    private readonly IAuthApiClient _authClient;
    private readonly IHttpClientFactory _httpClientFactory;

    public RegisterTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        _authClient = Substitute.For<IAuthApiClient>();
        _authClient.GetExternalProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        _httpClientFactory = Substitute.For<IHttpClientFactory>();

        Services.AddSingleton(_authClient);
        Services.AddSingleton(_httpClientFactory);
        Services.AddSingleton(new BrandingOptions());
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
    }

    [Fact]
    public void Renders_FormWithExpectedFields()
    {
        IRenderedComponent<Register> cut = Render<Register>();

        cut.Markup.Should().Contain("Create an account");
        cut.Find("input[placeholder='name@example.com']").Should().NotBeNull();
        cut.Find("input[placeholder='Create a password']").Should().NotBeNull();
        cut.Find("input[placeholder='Confirm your password']").Should().NotBeNull();
    }

    [Fact]
    public void Renders_TermsAndPrivacyCheckboxes()
    {
        IRenderedComponent<Register> cut = Render<Register>();

        cut.Markup.Should().Contain("Terms of Service");
        cut.Markup.Should().Contain("Privacy Policy");
    }

    [Fact]
    public void Submit_WithMismatchedPasswords_ShowsError()
    {
        IRenderedComponent<Register> cut = Render<Register>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");
        cut.Find("input[placeholder='Create a password']").Input("P@ssword1");
        cut.Find("input[placeholder='Confirm your password']").Input("Different1");

        // Check terms and privacy checkboxes (indices 1 and 2; index 0 is passwordless)
        IRefreshableElementCollection<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        checkboxes[1].Change(true);
        checkboxes[2].Change(true);

        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Passwords do not match.");
        _authClient.DidNotReceive().RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Submit_WithoutTermsAccepted_ShowsError()
    {
        IRenderedComponent<Register> cut = Render<Register>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");
        cut.Find("input[placeholder='Create a password']").Input("P@ssword1");
        cut.Find("input[placeholder='Confirm your password']").Input("P@ssword1");

        // Accept privacy but not terms
        IRefreshableElementCollection<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        checkboxes[2].Change(true);

        cut.Find("form").Submit();

        cut.Markup.Should().Contain("You must agree to the Terms of Service.");
        _authClient.DidNotReceive().RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Submit_WithoutPrivacyAccepted_ShowsError()
    {
        IRenderedComponent<Register> cut = Render<Register>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");
        cut.Find("input[placeholder='Create a password']").Input("P@ssword1");
        cut.Find("input[placeholder='Confirm your password']").Input("P@ssword1");

        // Accept terms but not privacy
        IRefreshableElementCollection<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        checkboxes[1].Change(true);

        cut.Find("form").Submit();

        cut.Markup.Should().Contain("You must agree to the Privacy Policy.");
        _authClient.DidNotReceive().RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Submit_WithValidData_NavigatesToVerifyEmail()
    {
        _authClient.RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: true));
        _authClient.GetMatchingOrganizationByDomainAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        IRenderedComponent<Register> cut = Render<Register>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");
        cut.Find("input[placeholder='Create a password']").Input("P@ssword1");
        cut.Find("input[placeholder='Confirm your password']").Input("P@ssword1");

        IRefreshableElementCollection<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        checkboxes[1].Change(true);
        checkboxes[2].Change(true);

        cut.Find("form").Submit();

        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.Uri.Should().Contain("/verify-email");
    }

    [Fact]
    public void Submit_WithEmptyEmail_ShowsError()
    {
        IRenderedComponent<Register> cut = Render<Register>();

        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Please enter your email address.");
    }

    [Fact]
    public void Submit_WithEmailTaken_ShowsError()
    {
        _authClient.RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: false, Error: "email_taken"));

        IRenderedComponent<Register> cut = Render<Register>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");
        cut.Find("input[placeholder='Create a password']").Input("P@ssword1");
        cut.Find("input[placeholder='Confirm your password']").Input("P@ssword1");

        IRefreshableElementCollection<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        checkboxes[1].Change(true);
        checkboxes[2].Change(true);

        cut.Find("form").Submit();

        cut.Markup.Should().Contain("An account with this email already exists.");
    }

    [Fact]
    public void Renders_SignInLink()
    {
        IRenderedComponent<Register> cut = Render<Register>();

        cut.Markup.Should().Contain("Already have an account?");
        cut.Find("a[href='/login']").Should().NotBeNull();
    }

    [Fact]
    public void Submit_WithReturnUrl_NavigatesToVerifyEmailWithReturnUrl()
    {
        _authClient.RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: true));
        _authClient.GetMatchingOrganizationByDomainAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.NavigateTo("/register?ReturnUrl=%2Fdashboard");

        IRenderedComponent<Register> cut = Render<Register>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");
        cut.Find("input[placeholder='Create a password']").Input("P@ssword1");
        cut.Find("input[placeholder='Confirm your password']").Input("P@ssword1");

        IRefreshableElementCollection<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        checkboxes[1].Change(true);
        checkboxes[2].Change(true);

        cut.Find("form").Submit();

        navMan.Uri.Should().Contain("/verify-email");
        navMan.Uri.Should().Contain("returnUrl");
    }

    [Fact]
    public void Submit_WithEmptyPassword_ShowsError()
    {
        IRenderedComponent<Register> cut = Render<Register>();

        cut.Find("input[placeholder='name@example.com']").Input("user@test.com");

        IRefreshableElementCollection<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        checkboxes[1].Change(true);
        checkboxes[2].Change(true);

        cut.Find("form").Submit();

        cut.Markup.Should().Contain("Please enter a password.");
    }
}
