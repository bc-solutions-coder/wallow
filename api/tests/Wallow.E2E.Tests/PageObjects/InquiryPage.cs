using Microsoft.Playwright;
using Wallow.E2E.Tests.Infrastructure;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class InquiryPage
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public InquiryPage(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/dashboard/inquiries");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await E2ETestBase.WaitForBlazorReadyAsync(_page);
        await _page.Locator("[data-testid='inquiry-name']")
            .WaitForAsync(new() { Timeout = 10_000 });
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.Locator("[data-testid='inquiry-name']").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task FillFormAsync(
        string name,
        string email,
        string message,
        string? phone = null,
        string? company = null,
        string? projectType = null,
        string? budgetRange = null,
        string? timeline = null)
    {
        await _page.Locator("[data-testid='inquiry-name']").FillAsync(name);
        await _page.Locator("[data-testid='inquiry-email']").FillAsync(email);

        if (phone is not null)
        {
            await _page.Locator("[data-testid='inquiry-phone']").FillAsync(phone);
        }

        if (company is not null)
        {
            await _page.Locator("[data-testid='inquiry-company']").FillAsync(company);
        }

        if (projectType is not null)
        {
            await _page.Locator("[data-testid='inquiry-project-type']").SelectOptionAsync(projectType);
        }

        if (budgetRange is not null)
        {
            await _page.Locator("[data-testid='inquiry-budget-range']").SelectOptionAsync(budgetRange);
        }

        if (timeline is not null)
        {
            await _page.Locator("[data-testid='inquiry-timeline']").SelectOptionAsync(timeline);
        }

        await _page.Locator("[data-testid='inquiry-message']").FillAsync(message);
    }

    public async Task SubmitInquiryAsync()
    {
        await _page.Locator("[data-testid='inquiry-submit']").ClickAsync();
        // Wait for server response via SignalR (not HTTP)
        await _page.Locator("[data-testid='inquiry-success'], [data-testid='inquiry-error']")
            .First.WaitForAsync(new() { Timeout = 15_000 });
    }

    public async Task<bool> IsSubmissionSuccessAsync()
    {
        ILocator success = _page.Locator("[data-testid='inquiry-success']");
        return await success.IsVisibleAsync();
    }

    public async Task<string?> GetErrorMessageAsync()
    {
        ILocator error = _page.Locator("[data-testid='inquiry-error']");
        bool isVisible = await error.IsVisibleAsync();
        if (!isVisible)
        {
            return null;
        }

        return await error.InnerTextAsync();
    }
}
