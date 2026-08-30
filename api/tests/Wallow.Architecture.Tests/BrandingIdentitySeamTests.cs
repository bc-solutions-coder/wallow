using System.Reflection;
using NetArchTest.Rules;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Branding hangs a sub-resource off Identity's org-scoped client surface, which makes it the
/// module most tempted to answer "does this client belong to this organization" by reaching for
/// OpenIddict directly — exactly the seam leak it once had. The answer lives behind
/// <c>IOrganizationClientDirectory</c> in Shared.Contracts; these tests pin every Branding layer
/// off OpenIddict so the seam cannot quietly reopen.
/// </summary>
public class BrandingIdentitySeamTests
{
    [Theory]
    [InlineData("Domain")]
    [InlineData("Application")]
    [InlineData("Infrastructure")]
    [InlineData("Api")]
    public void BrandingLayer_ShouldNotReference_OpenIddict(string layer)
    {
        Assembly assembly = Assembly.Load($"Wallow.Branding.{layer}");

        TestResult result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOn("OpenIddict")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Wallow.Branding.{layer} must resolve client ownership through Identity's public " +
            "contract (IOrganizationClientDirectory), never through OpenIddict. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }
}
