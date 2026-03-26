using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;
using Wallow.Auth.Models;
using Wallow.Auth.Services;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class MfaChallengeTests : BunitContext
{
    private readonly IAuthApiClient _authClient;

    public MfaChallengeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        _authClient = Substitute.For<IAuthApiClient>();
        Services.AddSingleton(_authClient);
        Services.AddSingleton(new BrandingOptions { AppName = "TestApp" });
    }

    private void NavigateWithChallengeToken(string token)
    {
        BunitNavigationManager navMan = Services.GetRequiredService<BunitNavigationManager>();
        navMan.NavigateTo($"/mfa/challenge?ChallengeToken={Uri.EscapeDataString(token)}");
    }

    [Fact]
    public void Renders_OtpInputFormWithChallengeToken()
    {
        NavigateWithChallengeToken("challenge-123");

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        cut.Markup.Should().Contain("Two-factor authentication");
        cut.Markup.Should().Contain("authenticator app");
        cut.Find("#code").Should().NotBeNull();
    }

    [Fact]
    public void MissingChallengeToken_ShowsError()
    {
        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        cut.Markup.Should().Contain("Missing challenge token");
    }

    [Fact]
    public async Task Submit_WithValidCode_CallsVerifyAndShowsSuccess()
    {
        _authClient.VerifyMfaChallengeAsync("challenge-123", "123456", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(true));

        NavigateWithChallengeToken("challenge-123");

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "123456" });
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Verification successful");
    }

    [Fact]
    public async Task Submit_WithEmptyCode_ShowsValidationError()
    {
        NavigateWithChallengeToken("challenge-123");

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Please enter the verification code");
    }

    [Fact]
    public void ToggleToBackupCodeMode_ChangesDescription()
    {
        NavigateWithChallengeToken("challenge-123");

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        AngleSharp.Dom.IElement toggleButton = cut.FindAll("button[type='button']")
            .First(b => b.TextContent.Contains("Use backup code instead"));
        toggleButton.Click();

        cut.Markup.Should().Contain("backup codes");
        cut.Markup.Should().Contain("Use authenticator code instead");
    }

    [Fact]
    public async Task BackupCodeMode_Submit_CallsUseBackupCode()
    {
        _authClient.UseBackupCodeAsync("challenge-123", "BACKUP-CODE", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(true));

        NavigateWithChallengeToken("challenge-123");

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        AngleSharp.Dom.IElement toggleButton = cut.FindAll("button[type='button']")
            .First(b => b.TextContent.Contains("Use backup code instead"));
        toggleButton.Click();

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "BACKUP-CODE" });
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Verification successful");
        await _authClient.Received(1).UseBackupCodeAsync("challenge-123", "BACKUP-CODE", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidCode_ShowsErrorMessage()
    {
        _authClient.VerifyMfaChallengeAsync("challenge-123", "wrong", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(false, Error: "invalid_code"));

        NavigateWithChallengeToken("challenge-123");

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "wrong" });
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Invalid verification code");
    }

    [Fact]
    public async Task ExpiredChallenge_ShowsError()
    {
        _authClient.VerifyMfaChallengeAsync("challenge-123", "123456", Arg.Any<CancellationToken>())
            .Returns(new AuthResponse(false, Error: "expired_challenge"));

        NavigateWithChallengeToken("challenge-123");

        IRenderedComponent<MfaChallenge> cut = Render<MfaChallenge>();

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "123456" });
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("Challenge expired");
    }
}
