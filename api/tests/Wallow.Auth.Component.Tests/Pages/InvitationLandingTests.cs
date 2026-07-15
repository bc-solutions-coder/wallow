using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;
using Wallow.Auth.Services;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class InvitationLandingTests : BunitContext
{
    private readonly IAuthApiClient _authApi;
    private readonly FakeLogger<InvitationLanding> _logger;

    public InvitationLandingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        _authApi = Substitute.For<IAuthApiClient>();
        _logger = new FakeLogger<InvitationLanding>();
        Services.AddSingleton(_authApi);
        Services.AddSingleton<ILogger<InvitationLanding>>(_logger);
        Services.AddSingleton(new BrandingOptions { AppName = "TestApp" });
    }

    private void NavigateWithToken(string? token = null)
    {
        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        string query = token is not null ? $"?Token={Uri.EscapeDataString(token)}" : "";
        navMan.NavigateTo($"/invitation{query}");
    }

    [Fact]
    public void MissingToken_ShowsError()
    {
        AddAuthorization();

        IRenderedComponent<InvitationLanding> cut = Render<InvitationLanding>();

        cut.Markup.Should().Contain("No invitation token provided");
    }

    [Fact]
    public void ValidToken_Unauthenticated_ShowsRegisterAndLoginLinks()
    {
        AddAuthorization();

        _authApi.VerifyInvitationAsync("test-token", Arg.Any<CancellationToken>())
            .Returns(new InvitationDetailsResponse(
                Guid.NewGuid(), "invited@example.com", "Pending",
                DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow, null));

        NavigateWithToken("test-token");

        IRenderedComponent<InvitationLanding> cut = Render<InvitationLanding>();

        cut.Markup.Should().Contain("invited@example.com");
        cut.Markup.Should().Contain("Create account");
        cut.Markup.Should().Contain("Sign in to accept");
    }

    [Fact]
    public void ValidToken_Authenticated_ShowsAcceptButton()
    {
        BunitAuthorizationContext authCtx = AddAuthorization();
        authCtx.SetAuthorized("testuser");

        _authApi.VerifyInvitationAsync("test-token", Arg.Any<CancellationToken>())
            .Returns(new InvitationDetailsResponse(
                Guid.NewGuid(), "invited@example.com", "Pending",
                DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow, null));

        NavigateWithToken("test-token");

        IRenderedComponent<InvitationLanding> cut = Render<InvitationLanding>();

        cut.Markup.Should().Contain("Yes, join");
    }

    [Fact]
    public async Task AcceptInvitation_CallsApi()
    {
        BunitAuthorizationContext authCtx = AddAuthorization();
        authCtx.SetAuthorized("testuser");

        _authApi.VerifyInvitationAsync("test-token", Arg.Any<CancellationToken>())
            .Returns(new InvitationDetailsResponse(
                Guid.NewGuid(), "invited@example.com", "Pending",
                DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow, null));
        _authApi.AcceptInvitationAsync("test-token", Arg.Any<CancellationToken>())
            .Returns(true);

        NavigateWithToken("test-token");

        IRenderedComponent<InvitationLanding> cut = Render<InvitationLanding>();

        AngleSharp.Dom.IElement acceptButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Yes, join"));
        await acceptButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        await _authApi.Received(1).AcceptInvitationAsync("test-token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ExpiredInvitation_ShowsExpiredMessage()
    {
        AddAuthorization();

        _authApi.VerifyInvitationAsync("test-token", Arg.Any<CancellationToken>())
            .Returns(new InvitationDetailsResponse(
                Guid.NewGuid(), "invited@example.com", "Expired",
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-8), null));

        NavigateWithToken("test-token");

        IRenderedComponent<InvitationLanding> cut = Render<InvitationLanding>();

        cut.Markup.Should().Contain("invitation has expired");
    }

    [Fact]
    public void NullInvitationResponse_ShowsInvalidMessage()
    {
        AddAuthorization();

        _authApi.VerifyInvitationAsync("test-token", Arg.Any<CancellationToken>())
            .Returns((InvitationDetailsResponse?)null);

        NavigateWithToken("test-token");

        IRenderedComponent<InvitationLanding> cut = Render<InvitationLanding>();

        cut.Markup.Should().Contain("not valid or has already been used");
    }

    [Fact]
    public void OnInitialized_WithToken_LogsPageInitialization()
    {
        AddAuthorization();

        _authApi.VerifyInvitationAsync("test-token", Arg.Any<CancellationToken>())
            .Returns(new InvitationDetailsResponse(
                Guid.NewGuid(), "invited@example.com", "Pending",
                DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow, null));

        NavigateWithToken("test-token");

        Render<InvitationLanding>();

        _logger.LogEntries.Should().ContainSingle(e =>
            e.LogLevel == LogLevel.Information &&
            e.FormattedMessage.Contains("OIDC InvitationLanding:") &&
            e.FormattedMessage.Contains("initialized"));
    }

    [Fact]
    public void OnInitialized_WithoutToken_LogsWarning()
    {
        AddAuthorization();

        Render<InvitationLanding>();

        _logger.LogEntries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.FormattedMessage.Contains("OIDC InvitationLanding:") &&
            e.FormattedMessage.Contains("no token"));
    }

    [Fact]
    public void OnInitialized_InvalidInvitation_LogsWarning()
    {
        AddAuthorization();

        _authApi.VerifyInvitationAsync("bad-token", Arg.Any<CancellationToken>())
            .Returns((InvitationDetailsResponse?)null);

        NavigateWithToken("bad-token");

        Render<InvitationLanding>();

        _logger.LogEntries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.FormattedMessage.Contains("OIDC InvitationLanding:") &&
            e.FormattedMessage.Contains("invalid"));
    }

    [Fact]
    public async Task AcceptInvitation_Success_LogsAccepted()
    {
        BunitAuthorizationContext authCtx = AddAuthorization();
        authCtx.SetAuthorized("testuser");

        _authApi.VerifyInvitationAsync("test-token", Arg.Any<CancellationToken>())
            .Returns(new InvitationDetailsResponse(
                Guid.NewGuid(), "invited@example.com", "Pending",
                DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow, null));
        _authApi.AcceptInvitationAsync("test-token", Arg.Any<CancellationToken>())
            .Returns(true);

        NavigateWithToken("test-token");

        IRenderedComponent<InvitationLanding> cut = Render<InvitationLanding>();

        AngleSharp.Dom.IElement acceptButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Yes, join"));
        await acceptButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _logger.LogEntries.Should().Contain(e =>
            e.LogLevel == LogLevel.Information &&
            e.FormattedMessage.Contains("OIDC InvitationLanding:") &&
            e.FormattedMessage.Contains("accepted"));
    }
}
