using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Auth.Components.Pages;
using Wallow.Auth.Configuration;
using Wallow.Auth.Services;

namespace Wallow.Auth.Component.Tests.Pages;

public sealed class MfaEnrollTests : BunitContext
{
    private readonly IAuthApiClient _authClient;

    public MfaEnrollTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ComponentFactories.Add(new StubComponentFactory());
        _authClient = Substitute.For<IAuthApiClient>();
        Services.AddSingleton(_authClient);
        Services.AddSingleton(new BrandingOptions { AppName = "TestApp" });
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
    public async Task BeginSetup_CallsApiAndShowsSecret()
    {
        _authClient.EnrollTotpAsync(Arg.Any<CancellationToken>())
            .Returns(new MfaEnrollResponse("TESTSECRET", "otpauth://totp/test"));

        IRenderedComponent<MfaEnroll> cut = Render<MfaEnroll>();

        AngleSharp.Dom.IElement button = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Begin setup"));
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Markup.Should().Contain("TESTSECRET");
        cut.Markup.Should().Contain("Verification code");
    }

    [Fact]
    public async Task BeginSetup_Failure_ShowsError()
    {
        _authClient.EnrollTotpAsync(Arg.Any<CancellationToken>())
            .Returns((MfaEnrollResponse?)null);

        IRenderedComponent<MfaEnroll> cut = Render<MfaEnroll>();

        AngleSharp.Dom.IElement button = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Begin setup"));
        await button.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cut.Markup.Should().Contain("Failed to start MFA enrollment");
    }

    [Fact]
    public async Task ConfirmCode_Success_ShowsBackupCodes()
    {
        _authClient.EnrollTotpAsync(Arg.Any<CancellationToken>())
            .Returns(new MfaEnrollResponse("TESTSECRET", "otpauth://totp/test"));

        _authClient.ConfirmEnrollmentAsync("TESTSECRET", "123456", Arg.Any<CancellationToken>())
            .Returns(new MfaConfirmEnrollmentResponse(true, ["CODE1", "CODE2", "CODE3"]));

        IRenderedComponent<MfaEnroll> cut = Render<MfaEnroll>();

        AngleSharp.Dom.IElement beginButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Begin setup"));
        await beginButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        AngleSharp.Dom.IElement codeInput = cut.Find("#code");
        await codeInput.InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "123456" });
        await cut.Find("form").SubmitAsync();

        cut.Markup.Should().Contain("MFA enabled successfully");
        cut.Markup.Should().Contain("CODE1");
        cut.Markup.Should().Contain("CODE2");
        cut.Markup.Should().Contain("CODE3");
    }
}
