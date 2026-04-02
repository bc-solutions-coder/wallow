using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;
using Wallow.Auth.Models;
using Wallow.Auth.Services;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class RegisterTests : BunitContext
{
    private readonly IAuthApiClient _authClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Register> _logger;

    public RegisterTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        _authClient = Substitute.For<IAuthApiClient>();
        _authClient.GetExternalProvidersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _logger = Substitute.For<ILogger<Register>>();

        Services.AddSingleton(_authClient);
        Services.AddSingleton(_httpClientFactory);
        Services.AddSingleton(new BrandingOptions());
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton(_logger);
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
    public async Task Submit_WithMismatchedPasswords_ShowsError()
    {
        IRenderedComponent<Register> cut = Render<Register>();

        await cut.Find("input[placeholder='name@example.com']").InputAsync(new ChangeEventArgs { Value = "user@test.com" });
        await cut.Find("input[placeholder='Create a password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });
        await cut.Find("input[placeholder='Confirm your password']").InputAsync(new ChangeEventArgs { Value = "Different1" });

        // Check terms and privacy checkboxes (indices 1 and 2; index 0 is passwordless)
        IReadOnlyList<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        await checkboxes[1].ChangeAsync(new ChangeEventArgs { Value = true });
        await checkboxes[2].ChangeAsync(new ChangeEventArgs { Value = true });

        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Passwords do not match.");
        await _authClient.DidNotReceive().RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithoutTermsAccepted_ShowsError()
    {
        IRenderedComponent<Register> cut = Render<Register>();

        await cut.Find("input[placeholder='name@example.com']").InputAsync(new ChangeEventArgs { Value = "user@test.com" });
        await cut.Find("input[placeholder='Create a password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });
        await cut.Find("input[placeholder='Confirm your password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });

        // Accept privacy but not terms
        IReadOnlyList<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        await checkboxes[2].ChangeAsync(new ChangeEventArgs { Value = true });

        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("You must agree to the Terms of Service.");
        await _authClient.DidNotReceive().RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithoutPrivacyAccepted_ShowsError()
    {
        IRenderedComponent<Register> cut = Render<Register>();

        await cut.Find("input[placeholder='name@example.com']").InputAsync(new ChangeEventArgs { Value = "user@test.com" });
        await cut.Find("input[placeholder='Create a password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });
        await cut.Find("input[placeholder='Confirm your password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });

        // Accept terms but not privacy
        IReadOnlyList<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        await checkboxes[1].ChangeAsync(new ChangeEventArgs { Value = true });

        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("You must agree to the Privacy Policy.");
        await _authClient.DidNotReceive().RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WithValidData_NavigatesToVerifyEmail()
    {
        _authClient.RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: true));
        _authClient.GetMatchingOrganizationByDomainAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        IRenderedComponent<Register> cut = Render<Register>();

        await cut.Find("input[placeholder='name@example.com']").InputAsync(new ChangeEventArgs { Value = "user@test.com" });
        await cut.Find("input[placeholder='Create a password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });
        await cut.Find("input[placeholder='Confirm your password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });

        IReadOnlyList<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        await checkboxes[1].ChangeAsync(new ChangeEventArgs { Value = true });
        await checkboxes[2].ChangeAsync(new ChangeEventArgs { Value = true });

        await cut.Find("form").SubmitAsync();

        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.Uri.Should().Contain("/verify-email");
    }

    [Fact]
    public async Task Submit_WithEmptyEmail_ShowsError()
    {
        IRenderedComponent<Register> cut = Render<Register>();

        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Please enter your email address.");
    }

    [Fact]
    public async Task Submit_WithEmailTaken_ShowsError()
    {
        _authClient.RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: false, Error: "email_taken"));

        IRenderedComponent<Register> cut = Render<Register>();

        await cut.Find("input[placeholder='name@example.com']").InputAsync(new ChangeEventArgs { Value = "user@test.com" });
        await cut.Find("input[placeholder='Create a password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });
        await cut.Find("input[placeholder='Confirm your password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });

        IReadOnlyList<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        await checkboxes[1].ChangeAsync(new ChangeEventArgs { Value = true });
        await checkboxes[2].ChangeAsync(new ChangeEventArgs { Value = true });

        await cut.Find("form").SubmitAsync();

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
    public async Task Submit_WithReturnUrl_NavigatesToVerifyEmailWithReturnUrl()
    {
        _authClient.RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: true));
        _authClient.GetMatchingOrganizationByDomainAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.NavigateTo("/register?ReturnUrl=%2Fdashboard");

        IRenderedComponent<Register> cut = Render<Register>();

        await cut.Find("input[placeholder='name@example.com']").InputAsync(new ChangeEventArgs { Value = "user@test.com" });
        await cut.Find("input[placeholder='Create a password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });
        await cut.Find("input[placeholder='Confirm your password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });

        IReadOnlyList<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        await checkboxes[1].ChangeAsync(new ChangeEventArgs { Value = true });
        await checkboxes[2].ChangeAsync(new ChangeEventArgs { Value = true });

        await cut.Find("form").SubmitAsync();

        navMan.Uri.Should().Contain("/verify-email");
        navMan.Uri.Should().Contain("returnUrl");
    }

    [Fact]
    public async Task Submit_WithEmptyPassword_ShowsError()
    {
        IRenderedComponent<Register> cut = Render<Register>();

        await cut.Find("input[placeholder='name@example.com']").InputAsync(new ChangeEventArgs { Value = "user@test.com" });

        IReadOnlyList<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        await checkboxes[1].ChangeAsync(new ChangeEventArgs { Value = true });
        await checkboxes[2].ChangeAsync(new ChangeEventArgs { Value = true });

        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Please enter a password.");
    }

    [Fact]
    public void Init_LogsPageInitialized()
    {
        Render<Register>();

        _logger.ShouldHaveLoggedMessage("OIDC Register:");
    }

    [Fact]
    public async Task Submit_WithValidData_LogsSuccessfulRegistration()
    {
        _authClient.RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: true));
        _authClient.GetMatchingOrganizationByDomainAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        IRenderedComponent<Register> cut = Render<Register>();

        await cut.Find("input[placeholder='name@example.com']").InputAsync(new ChangeEventArgs { Value = "user@test.com" });
        await cut.Find("input[placeholder='Create a password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });
        await cut.Find("input[placeholder='Confirm your password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });

        IReadOnlyList<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        await checkboxes[1].ChangeAsync(new ChangeEventArgs { Value = true });
        await checkboxes[2].ChangeAsync(new ChangeEventArgs { Value = true });

        await cut.Find("form").SubmitAsync();

        _logger.ShouldHaveLoggedMessage("OIDC Register:");
    }

    [Fact]
    public async Task Submit_WithEmailTaken_LogsFailure()
    {
        _authClient.RegisterAsync(Arg.Any<RegisterRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(Succeeded: false, Error: "email_taken"));

        IRenderedComponent<Register> cut = Render<Register>();

        await cut.Find("input[placeholder='name@example.com']").InputAsync(new ChangeEventArgs { Value = "user@test.com" });
        await cut.Find("input[placeholder='Create a password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });
        await cut.Find("input[placeholder='Confirm your password']").InputAsync(new ChangeEventArgs { Value = "P@ssword1" });

        IReadOnlyList<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        await checkboxes[1].ChangeAsync(new ChangeEventArgs { Value = true });
        await checkboxes[2].ChangeAsync(new ChangeEventArgs { Value = true });

        await cut.Find("form").SubmitAsync();

        _logger.ShouldHaveLoggedMessage("OIDC Register:");
    }
}
