using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;
using Wallow.E2E.Tests.Infrastructure;
using Wallow.E2E.Tests.PageObjects;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Flows;

[Trait("Category", "E2E")]
public sealed class StaticPagesFlowTests : E2ETestBase
{
    public StaticPagesFlowTests(DockerComposeFixture docker, PlaywrightFixture playwright, ITestOutputHelper output)
        : base(docker, playwright, output)
    {
    }

    [Fact]
    [Trait("E2EGroup", "Settings")]
    public async Task TermsPage_Loads_ShowsHeading()
    {
        TermsPage termsPage = new(Page, Docker.AuthBaseUrl);
        await termsPage.NavigateAsync();

        bool isLoaded = await termsPage.IsLoadedAsync();
        Assert.True(isLoaded, "Terms page should be loaded and show heading");
    }

    [Fact]
    [Trait("E2EGroup", "Settings")]
    public async Task PrivacyPage_Loads_ShowsHeading()
    {
        PrivacyPage privacyPage = new(Page, Docker.AuthBaseUrl);
        await privacyPage.NavigateAsync();

        bool isLoaded = await privacyPage.IsLoadedAsync();
        Assert.True(isLoaded, "Privacy page should be loaded and show heading");
    }

    [Fact]
    [Trait("E2EGroup", "Settings")]
    public async Task TermsPage_BackButton_NavigatesToRegister()
    {
        TermsPage termsPage = new(Page, Docker.AuthBaseUrl);
        await termsPage.NavigateAsync();

        await termsPage.ClickBackAsync();

        await Page.WaitForURLAsync(
            url => url.Contains("/register", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 10_000 });
    }

    [Fact]
    [Trait("E2EGroup", "Settings")]
    public async Task PrivacyPage_BackButton_NavigatesToRegister()
    {
        PrivacyPage privacyPage = new(Page, Docker.AuthBaseUrl);
        await privacyPage.NavigateAsync();

        await privacyPage.ClickBackAsync();

        await Page.WaitForURLAsync(
            url => url.Contains("/register", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 10_000 });
    }

    [Fact]
    [Trait("E2EGroup", "Settings")]
    public async Task ErrorPage_NoReason_ShowsGenericMessage()
    {
        ErrorPage errorPage = new(Page, Docker.AuthBaseUrl);
        await errorPage.NavigateAsync();

        bool isLoaded = await errorPage.IsLoadedAsync();
        Assert.True(isLoaded, "Error page should be loaded");

        string message = await errorPage.GetErrorMessageAsync();
        Assert.False(string.IsNullOrWhiteSpace(message), "Error message should not be empty");

        bool signOutVisible = await errorPage.IsSignOutLinkVisibleAsync();
        Assert.False(signOutVisible, "Sign-out link should not be visible for generic error");
    }

    [Fact]
    [Trait("E2EGroup", "Settings")]
    public async Task ErrorPage_NotAMemberReason_ShowsSignOutLink()
    {
        ErrorPage errorPage = new(Page, Docker.AuthBaseUrl);
        await errorPage.NavigateAsync("not_a_member");

        bool isLoaded = await errorPage.IsLoadedAsync();
        Assert.True(isLoaded, "Error page should be loaded");

        bool signOutVisible = await errorPage.IsSignOutLinkVisibleAsync();
        Assert.True(signOutVisible, "Sign-out link should be visible for not_a_member reason");
    }
}
