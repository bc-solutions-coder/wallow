using System.Text.RegularExpressions;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Guards the wallow-web dashboard E2E cutover (bead Wallow-ffpq.3.10) from the Blazor
/// URL/readiness contract to the React app (apps/wallow-web). Verified by static inspection
/// of the E2E test source, because exercising the real page objects would require the live
/// docker stack and all three apps. Two acceptance criteria are encoded:
/// <list type="number">
/// <item><c>AuthenticatedE2ETestBase</c> must navigate to the React BFF login entry
/// point (<c>/bff/login</c>), not the Blazor route (<c>/authentication/login</c>).</item>
/// <item>Every wallow-web dashboard page object must wait on the React readiness signal
/// (a <c>data-app-ready</c> based helper) instead of <c>WaitForBlazorReadyAsync</c>.</item>
/// </list>
/// </summary>
public class E2EWebReadinessSwapTests
{
    private static readonly string _apiRoot = FindApiRoot();

    private static readonly string _e2eRoot = Path.Combine(
        _apiRoot,
        "tests",
        "Wallow.E2E.Tests");

    private static readonly string _authenticatedBasePath = Path.Combine(
        _e2eRoot,
        "Infrastructure",
        "AuthenticatedE2ETestBase.cs");

    private static readonly string _e2eBasePath = Path.Combine(
        _e2eRoot,
        "Infrastructure",
        "E2ETestBase.cs");

    private static readonly string _pageObjectsRoot = Path.Combine(_e2eRoot, "PageObjects");

    // A readiness helper that polls document.body for [data-app-ready="true"] — the React
    // counterpart of the Blazor circuit's [data-blazor-ready]. Naming is the coordinator's
    // call (WaitForWebReadyAsync as a new web-specific helper, or a shared WaitForAppReadyAsync);
    // this regex accepts either so the green phase is not boxed into one name.
    private static readonly Regex _reactReadinessHelper = new(
        "WaitFor(Web|App)ReadyAsync",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// The seven wallow-web dashboard page objects that currently call
    /// <c>WaitForBlazorReadyAsync</c> and must swap to the React readiness signal. The
    /// already-cut-over wallow-auth page objects (Mfa*, VerifyEmailConfirm, InvitationLanding)
    /// are deliberately excluded.
    /// </summary>
    public static IEnumerable<object[]> DashboardPageObjects()
    {
        yield return new object[] { "AppRegistrationPage.cs" };
        yield return new object[] { "SettingsMfaSection.cs" };
        yield return new object[] { "OrganizationDetailPage.cs" };
        yield return new object[] { "InquiryPage.cs" };
        yield return new object[] { "SettingsProfileSection.cs" };
        yield return new object[] { "DashboardPage.cs" };
        yield return new object[] { "OrganizationPage.cs" };
    }

    [Fact]
    public void AuthenticatedE2ETestBase_ShouldNotNavigateToBlazorLoginRoute()
    {
        string source = File.ReadAllText(_authenticatedBasePath);

        source.Should().NotContain(
            "/authentication/login",
            "the Blazor login route has no React counterpart; once the stack points at the " +
            "React wallow-web app this route 404s and breaks authed E2E setup");
    }

    [Fact]
    public void AuthenticatedE2ETestBase_ShouldNavigateToBffLoginEntryPoint()
    {
        string source = File.ReadAllText(_authenticatedBasePath);

        source.Should().Contain(
            "/bff/login",
            "the React reference frontend enters the OIDC chain via the BFF login endpoint " +
            "(/bff/login), so authed E2E setup must start there");
    }

    [Fact]
    public void E2ETestBase_ShouldExposeReactWebReadinessHelper()
    {
        string source = File.ReadAllText(_e2eBasePath);

        _reactReadinessHelper.IsMatch(source).Should().BeTrue(
            "E2ETestBase must provide a wallow-web readiness helper (WaitForWebReadyAsync or a " +
            "shared WaitForAppReadyAsync) that polls document.body for [data-app-ready=\"true\"], " +
            "mirroring the existing WaitForAuthReadyAsync");

        source.Should().Contain(
            "data-app-ready",
            "the React readiness helper must poll the same [data-app-ready=\"true\"] marker the " +
            "wallow-web ReadyIndicator stamps onto document.body after hydration");
    }

    [Theory]
    [MemberData(nameof(DashboardPageObjects))]
    public void DashboardPageObject_ShouldNotCallBlazorReadiness(string fileName)
    {
        string source = File.ReadAllText(Path.Combine(_pageObjectsRoot, fileName));

        source.Should().NotContain(
            "WaitForBlazorReadyAsync",
            $"{fileName} targets the React wallow-web app, which never emits [data-blazor-ready]; " +
            "the Blazor readiness wait must be replaced by the React readiness signal");
    }

    [Theory]
    [MemberData(nameof(DashboardPageObjects))]
    public void DashboardPageObject_ShouldWaitOnReactReadiness(string fileName)
    {
        string source = File.ReadAllText(Path.Combine(_pageObjectsRoot, fileName));

        _reactReadinessHelper.IsMatch(source).Should().BeTrue(
            $"{fileName} must wait on the React readiness signal (WaitForWebReadyAsync or a shared " +
            "WaitForAppReadyAsync) so navigation synchronises on hydration, the way it previously " +
            "waited on WaitForBlazorReadyAsync");
    }

    private static string FindApiRoot()
    {
        string? directory = Directory.GetCurrentDirectory();

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "Directory.Packages.props")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not find the api root containing Directory.Packages.props");
    }
}
