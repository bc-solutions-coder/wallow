using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class AcceptTermsTests : BunitContext
{
    public AcceptTermsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        Services.AddSingleton(new BrandingOptions { AppName = "TestApp" });

        Dictionary<string, string?> configValues = new()
        {
            ["ApiBaseUrl"] = "http://localhost:5001"
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        Services.AddSingleton(config);
    }

    private void NavigateWithParams(string? returnUrl = null, string? email = null, string? name = null, string? error = null)
    {
        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        List<string> queryParams = new();
        if (returnUrl is not null)
        {
            queryParams.Add($"ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        }
        if (email is not null)
        {
            queryParams.Add($"Email={Uri.EscapeDataString(email)}");
        }
        if (name is not null)
        {
            queryParams.Add($"Name={Uri.EscapeDataString(name)}");
        }
        if (error is not null)
        {
            queryParams.Add($"Error={Uri.EscapeDataString(error)}");
        }
        string query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        navMan.NavigateTo($"/accept-terms{query}");
    }

    [Fact]
    public void Renders_TermsPageWithCheckboxesAndButton()
    {
        IRenderedComponent<AcceptTerms> cut = Render<AcceptTerms>();

        cut.Markup.Should().Contain("Almost there!");
        cut.Markup.Should().Contain("Terms of Service");
        cut.Markup.Should().Contain("Privacy Policy");
        cut.Markup.Should().Contain("Create Account");
    }

    [Fact]
    public void ShowsEmail_WhenProvided()
    {
        NavigateWithParams(email: "test@example.com", name: "Test User");

        IRenderedComponent<AcceptTerms> cut = Render<AcceptTerms>();

        cut.Markup.Should().Contain("test@example.com");
        cut.Markup.Should().Contain("Test User");
    }

    [Fact]
    public void ShowsError_WhenTermsRequired()
    {
        NavigateWithParams(error: "terms_required");

        IRenderedComponent<AcceptTerms> cut = Render<AcceptTerms>();

        cut.Markup.Should().Contain("You must accept the terms to continue");
    }

    [Fact]
    public void ShowsError_WhenSessionExpired()
    {
        NavigateWithParams(error: "session_expired");

        IRenderedComponent<AcceptTerms> cut = Render<AcceptTerms>();

        cut.Markup.Should().Contain("Your session has expired");
    }

    [Fact]
    public void AcceptButton_NavigatesWhenBothCheckboxesChecked()
    {
        NavigateWithParams(returnUrl: "/dashboard");

        IRenderedComponent<AcceptTerms> cut = Render<AcceptTerms>();

        IRefreshableElementCollection<AngleSharp.Dom.IElement> checkboxes = cut.FindAll("input[type='checkbox']");
        checkboxes[0].Change(true);
        checkboxes[1].Change(true);

        AngleSharp.Dom.IElement button = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Create Account"));
        button.Click();

        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.Uri.Should().Contain("complete-external-registration");
        navMan.Uri.Should().Contain("acceptedTerms=true");
    }

    [Fact]
    public void BackToSignIn_LinkPresent()
    {
        IRenderedComponent<AcceptTerms> cut = Render<AcceptTerms>();

        cut.Find("a[href='/login']").TextContent.Should().Contain("Back to sign in");
    }
}
