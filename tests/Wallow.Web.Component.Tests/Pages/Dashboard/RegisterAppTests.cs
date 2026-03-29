using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Web.Components.Pages.Dashboard;
using Wallow.Web.Configuration;
using Wallow.Web.Models;
using Wallow.Web.Services;

namespace Wallow.Web.Component.Tests.Pages.Dashboard;

public sealed class RegisterAppTests : BunitContext
{
    private readonly IAppRegistrationService _appService;

    public RegisterAppTests()
    {
        _appService = Substitute.For<IAppRegistrationService>();
        Services.AddSingleton(_appService);
        Services.AddSingleton(new BrandingOptions());
        BunitAuthorizationContext authContext = AddAuthorization();
        authContext.SetAuthorized("testuser");
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
    }

    [Fact]
    public void Render_ShowsFormFields()
    {
        IRenderedComponent<RegisterApp> cut = Render<RegisterApp>();

        cut.Markup.Should().Contain("App Name");
        cut.Markup.Should().Contain("Client Type");
        cut.Markup.Should().Contain("Redirect URIs");
        cut.Markup.Should().Contain("Scopes");
        cut.Markup.Should().Contain("Register App");
    }

    [Fact]
    public void Render_ShowsBackLink()
    {
        IRenderedComponent<RegisterApp> cut = Render<RegisterApp>();

        cut.Markup.Should().Contain("Back to Apps");
        cut.Markup.Should().Contain("/dashboard/apps");
    }

    [Fact]
    public async Task Submit_WithValidData_CallsServiceAndShowsSuccess()
    {
        RegisterAppResult successResult = new("new-client-id", "secret-123", "reg-token", true, null);
        _appService.RegisterAppAsync(Arg.Any<RegisterAppModel>(), Arg.Any<CancellationToken>())
            .Returns(successResult);

        IRenderedComponent<RegisterApp> cut = Render<RegisterApp>();

        AngleSharp.Dom.IElement nameInput = cut.Find("input");
        await nameInput.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "Test Application" });

        AngleSharp.Dom.IElement form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("App Registered Successfully");
            cut.Markup.Should().Contain("new-client-id");
            cut.Markup.Should().Contain("secret-123");
        });

        await _appService.Received(1).RegisterAppAsync(Arg.Any<RegisterAppModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_WhenServiceFails_ShowsErrorMessage()
    {
        RegisterAppResult failResult = new(null, null, null, false, "Registration failed");
        _appService.RegisterAppAsync(Arg.Any<RegisterAppModel>(), Arg.Any<CancellationToken>())
            .Returns(failResult);

        IRenderedComponent<RegisterApp> cut = Render<RegisterApp>();

        AngleSharp.Dom.IElement form = cut.Find("form");
        await cut.InvokeAsync(() => form.Submit());

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Registration failed");
        });
    }

    [Fact]
    public void Render_ShowsBrandingSection()
    {
        IRenderedComponent<RegisterApp> cut = Render<RegisterApp>();

        cut.Markup.Should().Contain("Branding (Optional)");
        cut.Markup.Should().Contain("Company Display Name");
        cut.Markup.Should().Contain("Tagline");
        cut.Markup.Should().Contain("Logo");
    }
}
