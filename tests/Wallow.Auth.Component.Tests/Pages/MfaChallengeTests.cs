using Bunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;
using Wallow.Auth.Models;
using Wallow.Auth.Services;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class MfaChallengeTests : BunitContext
{
    private readonly IAuthApiClient _authClient;
    private readonly FakeLogger<MfaChallenge> _logger;

    public MfaChallengeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        _authClient = Substitute.For<IAuthApiClient>();
        _logger = new FakeLogger<MfaChallenge>();
        Services.AddSingleton(_authClient);
        Services.AddSingleton<ILogger<MfaChallenge>>(_logger);
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddSingleton(new BrandingOptions { AppName = "TestApp" });
    }

    [Fact]
    public void Renders_OtpInputForm()
    {
        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        cut.Markup.Should().Contain("Two-factor authentication");
        cut.Markup.Should().Contain("authenticator app");
        cut.Find("#code").Should().NotBeNull();
    }

    [Fact]
    public async Task Submit_WithValidCode_CallsVerifyAndShowsSuccess()
    {
        _authClient.VerifyMfaChallengeAsync("123456", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(true));

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "123456" });
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Verification successful");
    }

    [Fact]
    public async Task Submit_WithEmptyCode_ShowsValidationError()
    {
        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Please enter the verification code");
    }

    [Fact]
    public async Task ToggleToBackupCodeMode_ChangesDescription()
    {
        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        AngleSharp.Dom.IElement toggleButton = cut.FindAll("button[type='button']")
            .First(b => b.TextContent.Contains("Use backup code instead"));
        await toggleButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Markup.Should().Contain("backup codes");
        cut.Markup.Should().Contain("Use authenticator code instead");
    }

    [Fact]
    public async Task BackupCodeMode_Submit_CallsUseBackupCode()
    {
        _authClient.UseBackupCodeAsync("BACKUP-CODE", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(true));

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        AngleSharp.Dom.IElement toggleButton = cut.FindAll("button[type='button']")
            .First(b => b.TextContent.Contains("Use backup code instead"));
        await toggleButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "BACKUP-CODE" });
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Verification successful");
        await _authClient.Received(1).UseBackupCodeAsync("BACKUP-CODE", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidCode_ShowsErrorMessage()
    {
        _authClient.VerifyMfaChallengeAsync("wrong", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(false, Error: "invalid_code"));

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "wrong" });
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Invalid verification code");
    }

    [Fact]
    public async Task ExpiredChallenge_ShowsError()
    {
        _authClient.VerifyMfaChallengeAsync("123456", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(false, Error: "expired_challenge"));

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "123456" });
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Challenge expired");
    }

    [Fact]
    public void OnInitialized_LogsPageInitialization()
    {
        Render<MfaChallenge>();

        _logger.LogEntries.Should().ContainSingle(e =>
            e.LogLevel == LogLevel.Information &&
            e.FormattedMessage.Contains("OIDC MfaChallenge:") &&
            e.FormattedMessage.Contains("initialized"));
    }

    [Fact]
    public async Task HandleVerify_Success_LogsVerificationResult()
    {
        _authClient.VerifyMfaChallengeAsync("123456", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(true));

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "123456" });
        await cut.Find("form").SubmitAsync();

        _logger.LogEntries.Should().Contain(e =>
            e.LogLevel == LogLevel.Information &&
            e.FormattedMessage.Contains("OIDC MfaChallenge:") &&
            e.FormattedMessage.Contains("verified"));
    }

    [Fact]
    public async Task HandleVerify_Failure_LogsWarning()
    {
        _authClient.VerifyMfaChallengeAsync("wrong", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(false, Error: "invalid_code"));

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "wrong" });
        await cut.Find("form").SubmitAsync();

        _logger.LogEntries.Should().Contain(e =>
            e.LogLevel == LogLevel.Warning &&
            e.FormattedMessage.Contains("OIDC MfaChallenge:") &&
            e.FormattedMessage.Contains("failed"));
    }

    [Fact]
    public async Task HandleVerify_BackupCode_LogsBackupCodeUsage()
    {
        _authClient.UseBackupCodeAsync("BACKUP-CODE", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(true));

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        AngleSharp.Dom.IElement toggleButton = cut.FindAll("button[type='button']")
            .First(b => b.TextContent.Contains("Use backup code instead"));
        await toggleButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "BACKUP-CODE" });
        await cut.Find("form").SubmitAsync();

        _logger.LogEntries.Should().Contain(e =>
            e.LogLevel == LogLevel.Information &&
            e.FormattedMessage.Contains("OIDC MfaChallenge:") &&
            e.FormattedMessage.Contains("backup code"));
    }
}
