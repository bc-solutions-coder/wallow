using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;
using Wallow.Auth.Services;
using Wallow.Tests.Common.Helpers;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class LogoutTests : BunitContext
{
    private readonly IAuthApiClient _authClient;
    private readonly ILogger<Logout> _logger;

    public LogoutTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        _authClient = Substitute.For<IAuthApiClient>();
        _logger = Substitute.For<ILogger<Logout>>();
        Services.AddSingleton(_authClient);
        Services.AddSingleton(new BrandingOptions { AppName = "TestApp" });
        Services.AddSingleton(_logger);

        Dictionary<string, string?> configValues = new()
        {
            ["ApiBaseUrl"] = "http://localhost:5001"
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        Services.AddSingleton(config);
    }

    private void NavigateWithParams(string? signedOut = null, string? postLogoutRedirectUri = null)
    {
        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        List<string> queryParams = new();
        if (signedOut is not null)
        {
            queryParams.Add($"signed_out={Uri.EscapeDataString(signedOut)}");
        }
        if (postLogoutRedirectUri is not null)
        {
            queryParams.Add($"post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}");
        }
        string query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        navMan.NavigateTo($"/logout{query}");
    }

    [Fact]
    public void Renders_SignOutConfirmationPage()
    {
        IRenderedComponent<Logout> cut = Render<Logout>();

        cut.Markup.Should().Contain("Sign out");
        cut.Markup.Should().Contain("Are you sure you want to sign out?");
    }

    [Fact]
    public void SignOutLink_PointsToApiLogout()
    {
        IRenderedComponent<Logout> cut = Render<Logout>();

        AngleSharp.Dom.IElement signOutLink = cut.FindAll("a")
            .First(a => a.TextContent.Trim() == "Sign out");
        signOutLink.GetAttribute("href").Should().Be("http://localhost:5001/connect/logout");
    }

    [Fact]
    public void SignedOutState_ShowsSuccessMessage()
    {
        NavigateWithParams(signedOut: "true");

        IRenderedComponent<Logout> cut = Render<Logout>();

        cut.Markup.Should().Contain("Signed out");
        cut.Markup.Should().Contain("successfully signed out");
    }

    [Fact]
    public void SignedOut_WithValidRedirectUri_ShowsReturnLink()
    {
        _authClient.ValidateRedirectUriAsync("https://app.example.com", Arg.Any<CancellationToken>())
            .Returns(true);

        NavigateWithParams(signedOut: "true", postLogoutRedirectUri: "https://app.example.com");

        IRenderedComponent<Logout> cut = Render<Logout>();

        cut.Markup.Should().Contain("Return to application");
        AngleSharp.Dom.IElement returnLink = cut.FindAll("a")
            .First(a => a.TextContent.Contains("Return to application"));
        returnLink.GetAttribute("href").Should().Be("https://app.example.com");
    }

    [Fact]
    public void SignedOut_WithInvalidRedirectUri_DoesNotShowReturnLink()
    {
        _authClient.ValidateRedirectUriAsync("https://evil.com", Arg.Any<CancellationToken>())
            .Returns(false);

        NavigateWithParams(signedOut: "true", postLogoutRedirectUri: "https://evil.com");

        IRenderedComponent<Logout> cut = Render<Logout>();

        cut.Markup.Should().NotContain("Return to application");
    }

    [Fact]
    public void SignOutLink_WithRedirectUri_IncludesEncodedParam()
    {
        NavigateWithParams(postLogoutRedirectUri: "https://app.example.com");

        IRenderedComponent<Logout> cut = Render<Logout>();

        AngleSharp.Dom.IElement signOutLink = cut.FindAll("a")
            .First(a => a.TextContent.Trim() == "Sign out");
        signOutLink.GetAttribute("href").Should().Contain("post_logout_redirect_uri=");
    }

    [Fact]
    public void BackToSignIn_LinkPresent()
    {
        IRenderedComponent<Logout> cut = Render<Logout>();

        cut.Find("a[href='/login']").TextContent.Should().Contain("Back to sign in");
    }

    [Fact]
    public void Init_LogsPageInitialized()
    {
        Render<Logout>();

        _logger.ShouldHaveLoggedMessage("OIDC Logout:");
    }

    [Fact]
    public void SignedOut_LogsSignedOutState()
    {
        NavigateWithParams(signedOut: "true");

        Render<Logout>();

        _logger.ShouldHaveLoggedMessage("OIDC Logout:");
    }

    [Fact]
    public void SignedOut_WithRedirectUri_LogsRedirectValidation()
    {
        _authClient.ValidateRedirectUriAsync("https://app.example.com", Arg.Any<CancellationToken>())
            .Returns(true);

        NavigateWithParams(signedOut: "true", postLogoutRedirectUri: "https://app.example.com");

        Render<Logout>();

        _logger.ShouldHaveLoggedMessage("OIDC Logout:");
    }
}
