using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Web.Components;

namespace Wallow.Web.Component.Tests;

public sealed class RedirectToLoginTests : BunitContext
{
    [Fact]
    public void Render_NavigatesToLoginPage()
    {
        Render<RedirectToLogin>();

        BunitNavigationManager nav = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        nav.History.Should().Contain(h => h.Uri.Contains("/authentication/login"));
    }
}
