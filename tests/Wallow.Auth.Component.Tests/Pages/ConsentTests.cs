using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Services;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class ConsentTests : BunitContext
{
    private readonly IAuthApiClient _authClient;
    private readonly FakeLogger<Consent> _logger;

    public ConsentTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());

        _authClient = Substitute.For<IAuthApiClient>();
        _logger = new FakeLogger<Consent>();
        Services.AddSingleton(_authClient);
        Services.AddSingleton<ILogger<Consent>>(_logger);
    }

    private void NavigateToConsent(string? returnUrl = null, string? clientId = null)
    {
        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        List<string> queryParams = new();
        if (returnUrl is not null)
        {
            queryParams.Add($"ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        }
        if (clientId is not null)
        {
            queryParams.Add($"client_id={Uri.EscapeDataString(clientId)}");
        }
        string query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        navMan.NavigateTo($"/consent{query}");
    }

    [Fact]
    public void Renders_HeadingWithDisplayName_WhenConsentInfoReturned()
    {
        ConsentInfo consentInfo = new(
            ClientId: "my-app",
            DisplayName: "My Application",
            LogoUrl: null,
            RequestedScopes: new List<ConsentScopeInfo>
            {
                new("openid", "Access your identity"),
                new("profile", "Access your profile")
            });

        _authClient.GetConsentInfoAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(consentInfo);

        NavigateToConsent(returnUrl: "/callback", clientId: "my-app");

        IRenderedComponent<Consent> cut = Render<Consent>();

        AngleSharp.Dom.IElement heading = cut.Find("[data-testid='consent-heading']");
        heading.TextContent.Should().Contain("My Application");
    }

    [Fact]
    public void Renders_AllRequestedScopes_WhenConsentInfoReturned()
    {
        ConsentInfo consentInfo = new(
            ClientId: "my-app",
            DisplayName: "My Application",
            LogoUrl: null,
            RequestedScopes: new List<ConsentScopeInfo>
            {
                new("openid", "Access your identity"),
                new("profile", "Access your profile"),
                new("email", "Access your email address")
            });

        _authClient.GetConsentInfoAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(consentInfo);

        NavigateToConsent(returnUrl: "/callback", clientId: "my-app");

        IRenderedComponent<Consent> cut = Render<Consent>();

        AngleSharp.Dom.IElement scopesContainer = cut.Find("[data-testid='consent-scopes']");
        scopesContainer.TextContent.Should().Contain("openid");
        scopesContainer.TextContent.Should().Contain("profile");
        scopesContainer.TextContent.Should().Contain("email");
    }

    [Fact]
    public void Renders_ApproveAndDenyButtons_WhenConsentInfoReturned()
    {
        ConsentInfo consentInfo = new(
            ClientId: "my-app",
            DisplayName: "My Application",
            LogoUrl: null,
            RequestedScopes: new List<ConsentScopeInfo>
            {
                new("openid", "Access your identity")
            });

        _authClient.GetConsentInfoAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(consentInfo);

        NavigateToConsent(returnUrl: "/callback", clientId: "my-app");

        IRenderedComponent<Consent> cut = Render<Consent>();

        AngleSharp.Dom.IElement approveButton = cut.Find("[data-testid='consent-approve']");
        AngleSharp.Dom.IElement denyButton = cut.Find("[data-testid='consent-deny']");
        approveButton.Should().NotBeNull();
        denyButton.Should().NotBeNull();
    }

    [Fact]
    public void Renders_ErrorAndNoButtons_WhenConsentInfoIsNull()
    {
        _authClient.GetConsentInfoAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns((ConsentInfo?)null);

        NavigateToConsent(returnUrl: "/callback", clientId: "my-app");

        IRenderedComponent<Consent> cut = Render<Consent>();

        AngleSharp.Dom.IElement errorElement = cut.Find("[data-testid='consent-error']");
        errorElement.Should().NotBeNull();

        Action findApprove = () => cut.Find("[data-testid='consent-approve']");
        findApprove.Should().Throw<Bunit.ElementNotFoundException>();

        Action findDeny = () => cut.Find("[data-testid='consent-deny']");
        findDeny.Should().Throw<Bunit.ElementNotFoundException>();
    }

    [Fact]
    public async Task ClickApprove_NavigatesWithConsentGranted()
    {
        ConsentInfo consentInfo = new(
            ClientId: "my-app",
            DisplayName: "My Application",
            LogoUrl: null,
            RequestedScopes: new List<ConsentScopeInfo>
            {
                new("openid", "Access your identity")
            });

        _authClient.GetConsentInfoAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(consentInfo);

        NavigateToConsent(returnUrl: "/callback", clientId: "my-app");

        IRenderedComponent<Consent> cut = Render<Consent>();

        AngleSharp.Dom.IElement approveButton = cut.Find("[data-testid='consent-approve']");
        await approveButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.Uri.Should().Contain("/callback");
        navMan.Uri.Should().Contain("consent_granted=true");
    }

    [Fact]
    public async Task ClickDeny_NavigatesWithConsentDenied()
    {
        ConsentInfo consentInfo = new(
            ClientId: "my-app",
            DisplayName: "My Application",
            LogoUrl: null,
            RequestedScopes: new List<ConsentScopeInfo>
            {
                new("openid", "Access your identity")
            });

        _authClient.GetConsentInfoAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(consentInfo);

        NavigateToConsent(returnUrl: "/callback", clientId: "my-app");

        IRenderedComponent<Consent> cut = Render<Consent>();

        AngleSharp.Dom.IElement denyButton = cut.Find("[data-testid='consent-deny']");
        await denyButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.Uri.Should().Contain("/callback");
        navMan.Uri.Should().Contain("consent_denied=true");
    }

    [Fact]
    public async Task ClickApprove_WhenReturnUrlHasQueryString_AppendsWithAmpersand()
    {
        ConsentInfo consentInfo = new(
            ClientId: "my-app",
            DisplayName: "My Application",
            LogoUrl: null,
            RequestedScopes: new List<ConsentScopeInfo>
            {
                new("openid", "Access your identity")
            });

        _authClient.GetConsentInfoAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(consentInfo);

        NavigateToConsent(returnUrl: "/callback?state=abc123", clientId: "my-app");

        IRenderedComponent<Consent> cut = Render<Consent>();

        AngleSharp.Dom.IElement approveButton = cut.Find("[data-testid='consent-approve']");
        await approveButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        string uri = navMan.Uri;
        uri.Should().Contain("/callback?state=abc123&consent_granted=true");
        // Must not contain two '?' characters in the path portion
        int questionMarkCount = uri.Count(c => c == '?');
        questionMarkCount.Should().Be(1);
    }

    [Fact]
    public async Task ClickDeny_WhenReturnUrlHasQueryString_AppendsWithAmpersand()
    {
        ConsentInfo consentInfo = new(
            ClientId: "my-app",
            DisplayName: "My Application",
            LogoUrl: null,
            RequestedScopes: new List<ConsentScopeInfo>
            {
                new("openid", "Access your identity")
            });

        _authClient.GetConsentInfoAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(consentInfo);

        NavigateToConsent(returnUrl: "/callback?state=xyz", clientId: "my-app");

        IRenderedComponent<Consent> cut = Render<Consent>();

        AngleSharp.Dom.IElement denyButton = cut.Find("[data-testid='consent-deny']");
        await denyButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        string uri = navMan.Uri;
        uri.Should().Contain("/callback?state=xyz&consent_denied=true");
        int questionMarkCount = uri.Count(c => c == '?');
        questionMarkCount.Should().Be(1);
    }

    [Fact]
    public void OnInitialized_WithClientId_LogsPageInitialization()
    {
        _authClient.GetConsentInfoAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ConsentInfo("my-app", "My Application", null, new List<ConsentScopeInfo> { new("openid", "Identity") }));

        NavigateToConsent(returnUrl: "/callback", clientId: "my-app");

        Render<Consent>();

        _logger.LogEntries.Should().ContainSingle(e =>
            e.LogLevel == LogLevel.Information &&
            e.FormattedMessage.Contains("OIDC Consent:") &&
            e.FormattedMessage.Contains("initialized"));
    }

    [Fact]
    public void OnInitialized_WithoutClientId_LogsWarning()
    {
        Render<Consent>();

        _logger.LogEntries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.FormattedMessage.Contains("OIDC Consent:") &&
            e.FormattedMessage.Contains("no client"));
    }

    [Fact]
    public async Task Approve_LogsConsentGranted()
    {
        ConsentInfo consentInfo = new("my-app", "My Application", null,
            new List<ConsentScopeInfo> { new("openid", "Identity") });
        _authClient.GetConsentInfoAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(consentInfo);

        NavigateToConsent(returnUrl: "/callback", clientId: "my-app");

        IRenderedComponent<Consent> cut = Render<Consent>();

        AngleSharp.Dom.IElement approveButton = cut.Find("[data-testid='consent-approve']");
        await approveButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _logger.LogEntries.Should().Contain(e =>
            e.LogLevel == LogLevel.Information &&
            e.FormattedMessage.Contains("OIDC Consent:") &&
            e.FormattedMessage.Contains("approved"));
    }

    [Fact]
    public async Task Deny_LogsConsentDenied()
    {
        ConsentInfo consentInfo = new("my-app", "My Application", null,
            new List<ConsentScopeInfo> { new("openid", "Identity") });
        _authClient.GetConsentInfoAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(consentInfo);

        NavigateToConsent(returnUrl: "/callback", clientId: "my-app");

        IRenderedComponent<Consent> cut = Render<Consent>();

        AngleSharp.Dom.IElement denyButton = cut.Find("[data-testid='consent-deny']");
        await denyButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _logger.LogEntries.Should().Contain(e =>
            e.LogLevel == LogLevel.Information &&
            e.FormattedMessage.Contains("OIDC Consent:") &&
            e.FormattedMessage.Contains("denied"));
    }
}
