using Bunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;
using Wallow.Auth.Services;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class MfaEnrollTests : BunitContext
{
    private readonly IAuthApiClient _authClient;
    private readonly FakeLogger<MfaEnroll> _logger;

    public MfaEnrollTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        _authClient = Substitute.For<IAuthApiClient>();
        _logger = new FakeLogger<MfaEnroll>();
        Services.AddSingleton(_authClient);
        Services.AddSingleton<ILogger<MfaEnroll>>(_logger);
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton(new BrandingOptions { AppName = "TestApp" });
        Services.AddSingleton(Substitute.For<IHttpContextAccessor>());
        Services.AddSingleton(new ApiCookieJar());
        AddBunitPersistentComponentState();
    }

    [Fact]
    public void Renders_InitialSetupPageWithBeginButton()
    {
        IRenderedComponent<MfaEnroll> cut = Render<MfaEnroll>();

        cut.Markup.Should().Contain("Set up two-factor authentication");
        cut.Markup.Should().Contain("Begin setup");
        cut.Markup.Should().Contain("authenticator app");
    }

    [Fact]
    public void BeginSetup_CallsApiAndShowsSecret()
    {
        _authClient.EnrollTotpAsync(Arg.Any<CancellationToken>())
            .Returns(new MfaEnrollResponse("TESTSECRET", "otpauth://totp/test"));

        // Component auto-enrolls in OnInitializedAsync when no persisted state exists
        IRenderedComponent<MfaEnroll> cut = Render<MfaEnroll>();

        cut.Markup.Should().Contain("TESTSECRET");
        cut.Markup.Should().Contain("Verification code");
    }

    [Fact]
    public void BeginSetup_Failure_ShowsError()
    {
        _authClient.EnrollTotpAsync(Arg.Any<CancellationToken>())
            .Returns((MfaEnrollResponse?)null);

        IRenderedComponent<MfaEnroll> cut = Render<MfaEnroll>();

        cut.Markup.Should().Contain("Failed to start MFA enrollment");
    }

    [Fact]
    public async Task ConfirmCode_Success_ShowsBackupCodes()
    {
        _authClient.EnrollTotpAsync(Arg.Any<CancellationToken>())
            .Returns(new MfaEnrollResponse("TESTSECRET", "otpauth://totp/test"));

        _authClient.ConfirmEnrollmentAsync("TESTSECRET", "123456", Arg.Any<CancellationToken>())
            .Returns(new MfaConfirmEnrollmentResponse(true, ["CODE1", "CODE2", "CODE3"]));

        // Component auto-enrolls and shows the secret/QR code view
        IRenderedComponent<MfaEnroll> cut = Render<MfaEnroll>();

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "123456" });
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("MFA enabled successfully");
        cut.Markup.Should().Contain("CODE1");
        cut.Markup.Should().Contain("CODE2");
        cut.Markup.Should().Contain("CODE3");
    }

    [Fact]
    public void OnInitialized_LogsPageInitialization()
    {
        _authClient.EnrollTotpAsync(Arg.Any<CancellationToken>())
            .Returns(new MfaEnrollResponse("SECRET", "otpauth://totp/test"));

        Render<MfaEnroll>();

        _logger.LogEntries.Should().ContainSingle(e =>
            e.LogLevel == LogLevel.Information &&
            e.FormattedMessage.Contains("OIDC MfaEnroll:") &&
            e.FormattedMessage.Contains("initialized"));
    }

    [Fact]
    public void HandleStartEnroll_Success_LogsEnrollmentStarted()
    {
        _authClient.EnrollTotpAsync(Arg.Any<CancellationToken>())
            .Returns(new MfaEnrollResponse("SECRET", "otpauth://totp/test"));

        Render<MfaEnroll>();

        _logger.LogEntries.Should().Contain(e =>
            e.LogLevel == LogLevel.Information &&
            e.FormattedMessage.Contains("OIDC MfaEnroll:") &&
            e.FormattedMessage.Contains("secret retrieved"));
    }

    [Fact]
    public void HandleStartEnroll_Failure_LogsWarning()
    {
        _authClient.EnrollTotpAsync(Arg.Any<CancellationToken>())
            .Returns((MfaEnrollResponse?)null);

        Render<MfaEnroll>();

        _logger.LogEntries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.FormattedMessage.Contains("OIDC MfaEnroll:") &&
            e.FormattedMessage.Contains("failed"));
    }

    [Fact]
    public async Task ConfirmCode_Success_LogsEnrollmentConfirmed()
    {
        _authClient.EnrollTotpAsync(Arg.Any<CancellationToken>())
            .Returns(new MfaEnrollResponse("TESTSECRET", "otpauth://totp/test"));

        _authClient.ConfirmEnrollmentAsync("TESTSECRET", "123456", Arg.Any<CancellationToken>())
            .Returns(new MfaConfirmEnrollmentResponse(true, ["CODE1"]));

        IRenderedComponent<MfaEnroll> cut = Render<MfaEnroll>();

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "123456" });
        await cut.Find("form").SubmitAsync();

        _logger.LogEntries.Should().Contain(e =>
            e.LogLevel == LogLevel.Information &&
            e.FormattedMessage.Contains("OIDC MfaEnroll:") &&
            e.FormattedMessage.Contains("confirmed"));
    }

    [Fact]
    public async Task ConfirmCode_Failure_LogsWarning()
    {
        _authClient.EnrollTotpAsync(Arg.Any<CancellationToken>())
            .Returns(new MfaEnrollResponse("TESTSECRET", "otpauth://totp/test"));

        _authClient.ConfirmEnrollmentAsync("TESTSECRET", "000000", Arg.Any<CancellationToken>())
            .Returns(new MfaConfirmEnrollmentResponse(false, null, "invalid_code"));

        IRenderedComponent<MfaEnroll> cut = Render<MfaEnroll>();

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "000000" });
        await cut.Find("form").SubmitAsync();

        _logger.LogEntries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.FormattedMessage.Contains("OIDC MfaEnroll:") &&
            e.FormattedMessage.Contains("confirmation failed"));
    }
}
