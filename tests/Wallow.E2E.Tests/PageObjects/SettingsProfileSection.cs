using Microsoft.Playwright;
using Wallow.E2E.Tests.Infrastructure;

namespace Wallow.E2E.Tests.PageObjects;

public sealed class SettingsProfileSection
{
    private readonly IPage _page;
    private readonly string _baseUrl;

    public SettingsProfileSection(IPage page, string baseUrl)
    {
        _page = page;
        _baseUrl = baseUrl;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"{_baseUrl}/dashboard/settings");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await E2ETestBase.WaitForBlazorReadyAsync(_page);
        await _page.GetByTestId("settings-heading").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 10_000 });
    }

    public async Task<bool> IsLoadedAsync()
    {
        try
        {
            await _page.GetByTestId("settings-heading").WaitForAsync(new() { Timeout = 10_000 });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task<string> GetProfileNameAsync()
    {
        return await _page.GetByTestId("settings-profile-name").InnerTextAsync();
    }

    public async Task<string> GetProfileEmailAsync()
    {
        return await _page.GetByTestId("settings-profile-email").InnerTextAsync();
    }

    public async Task<IReadOnlyList<string>> GetProfileRolesAsync()
    {
        ILocator rolesContainer = _page.GetByTestId("settings-profile-roles");
        IReadOnlyList<ILocator> roleElements = await rolesContainer.Locator("[data-testid]").AllAsync();
        List<string> roles = [];
        foreach (ILocator role in roleElements)
        {
            roles.Add(await role.InnerTextAsync());
        }

        return roles;
    }
}
