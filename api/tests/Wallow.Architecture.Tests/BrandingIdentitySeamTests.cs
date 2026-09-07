using System.Reflection;
using NetArchTest.Rules;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Checks that Branding layers do not depend on OpenIddict. Client ownership belongs behind the shared directory contract.
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
