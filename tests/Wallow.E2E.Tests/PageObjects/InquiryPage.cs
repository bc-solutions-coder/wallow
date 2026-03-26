using Microsoft.Playwright;

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
    }

    public async Task<bool> IsLoadedAsync()
    {
        ILocator heading = _page.Locator("h1:has-text('Submit an Inquiry')");
        return await heading.IsVisibleAsync();
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
        ILocator nameInput = _page.Locator("label:has-text('Name') + input");
        await nameInput.FillAsync(name);

        ILocator emailInput = _page.Locator("label:has-text('Email') + input");
        await emailInput.FillAsync(email);

        if (phone is not null)
        {
            ILocator phoneInput = _page.Locator("input[placeholder='Optional']").First;
            await phoneInput.FillAsync(phone);
        }

        if (company is not null)
        {
            ILocator companyInput = _page.Locator("input[placeholder='Optional']").Last;
            await companyInput.FillAsync(company);
        }

        if (projectType is not null)
        {
            ILocator projectTypeSelect = _page.Locator("select").First;
            await projectTypeSelect.SelectOptionAsync(projectType);
        }

        if (budgetRange is not null)
        {
            ILocator budgetSelect = _page.Locator("select").Nth(1);
            await budgetSelect.SelectOptionAsync(budgetRange);
        }

        if (timeline is not null)
        {
            ILocator timelineSelect = _page.Locator("select").Nth(2);
            await timelineSelect.SelectOptionAsync(timeline);
        }

        ILocator messageTextarea = _page.Locator("textarea");
        await messageTextarea.FillAsync(message);
    }

    public async Task SubmitInquiryAsync()
    {
        await _page.Locator("button[type='submit']").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> IsSubmissionSuccessAsync()
    {
        ILocator successHeading = _page.Locator("text=Inquiry Submitted");
        return await successHeading.IsVisibleAsync();
    }

    public async Task<string?> GetErrorMessageAsync()
    {
        ILocator errorContainer = _page.Locator(".bg-red-50 p");
        bool isVisible = await errorContainer.IsVisibleAsync();
        if (!isVisible)
        {
            return null;
        }

        return await errorContainer.InnerTextAsync();
    }

    public async Task ClickSubmitAnotherAsync()
    {
        await _page.Locator("button:has-text('Submit Another')").ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }
}
