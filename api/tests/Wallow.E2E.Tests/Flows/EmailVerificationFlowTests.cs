using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

[Trait("Category", "E2E")]
public sealed class EmailVerificationFlowTests : E2ETestBase
{
    public EmailVerificationFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright, ITestOutputHelper output)
        : base(docker, playwright, output)
    {
    }

    [Fact]
    [Trait("E2EGroup", "EmailVerification")]
    public async Task EmailVerification_HappyPath_RegisterThenVerifyViaMailpit()
    {
        UnverifiedTestUser unverified = await TestUserFactory.CreateUnverifiedAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        VerifyEmailConfirmPage confirmPage = new(Page, Docker.AuthBaseUrl);
        await confirmPage.NavigateToLinkAsync(unverified.VerificationLink);
        await confirmPage.WaitForSuccessAsync();

        string successText = await confirmPage.GetSuccessTextAsync();
        Assert.False(string.IsNullOrEmpty(successText), "Success text should be present after email verification");
    }

    [Fact]
    [Trait("E2EGroup", "EmailVerification")]
    public async Task EmailVerification_InvalidToken_ShowsError()
    {
        VerifyEmailConfirmPage confirmPage = new(Page, Docker.AuthBaseUrl);
        await confirmPage.NavigateAsync(token: "invalid-token-abc", email: "notreal@test.local");
        await confirmPage.WaitForErrorAsync();

        string errorText = await confirmPage.GetErrorTextAsync();
        Assert.Contains("invalid", errorText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("E2EGroup", "EmailVerification")]
    public async Task EmailVerification_AlreadyVerified_HandledGracefully()
    {
        // CreateAsync registers and verifies the user via HTTP GET on the verification link
        TestUser user = await TestUserFactory.CreateAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        // The verification email is still in Mailpit even though the link was already consumed
        string verificationLink = await MailpitHelper.SearchForLinkAsync(
            Docker.MailpitBaseUrl, user.Email, "verify");

        if (string.IsNullOrEmpty(verificationLink))
        {
            verificationLink = await MailpitHelper.SearchForLinkAsync(
                Docker.MailpitBaseUrl, user.Email, "confirm", maxRetries: 3, pollIntervalSeconds: 1);
        }

        Assert.False(string.IsNullOrEmpty(verificationLink), "Verification link should exist in Mailpit");

        // Navigate to the already-used link -- expect error since token is consumed, but no crash
        VerifyEmailConfirmPage confirmPage = new(Page, Docker.AuthBaseUrl);
        await confirmPage.NavigateToLinkAsync(verificationLink);

        bool loaded = await confirmPage.IsLoadedAsync();
        Assert.True(loaded, "Page should load without crashing for an already-used verification link");

        // Either success or error is acceptable -- the requirement is no crash
        string successText;
        try { successText = await confirmPage.GetSuccessTextAsync(); }
        catch { successText = string.Empty; }

        string errorText;
        try { errorText = await confirmPage.GetErrorTextAsync(); }
        catch { errorText = string.Empty; }

        bool hasMeaningfulContent = !string.IsNullOrEmpty(successText) || !string.IsNullOrEmpty(errorText);
        Assert.True(hasMeaningfulContent, "Page should display either a success or error message for an already-verified link");
    }

    [Fact]
    [Trait("E2EGroup", "EmailVerification")]
    public async Task EmailVerification_UnverifiedLogin_ShowsError()
    {
        UnverifiedTestUser unverified = await TestUserFactory.CreateUnverifiedAsync(Docker.ApiBaseUrl, Docker.MailpitBaseUrl);

        LoginPage loginPage = new(Page, Docker.AuthBaseUrl);
        await loginPage.NavigateAsync();
        await loginPage.FillEmailAsync(unverified.Email);
        await loginPage.FillPasswordAsync(unverified.Password);
        await loginPage.SubmitAsync();

        bool errorVisible = await loginPage.IsErrorVisibleAsync();
        Assert.True(errorVisible, "Error message should be visible for unverified email login");

        string? errorMessage = await loginPage.GetErrorMessageAsync();
        Assert.False(string.IsNullOrEmpty(errorMessage), "Error message text should not be empty");
    }
}
