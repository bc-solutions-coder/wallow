using System.Reflection;
using NetArchTest.Rules;

#pragma warning disable CA1024 // MemberData source methods cannot be properties
#pragma warning disable CA1310 // String comparison in LINQ lambdas over type names is culture-safe

namespace Foundry.Architecture.Tests;

public class CleanArchitectureTests
{
    public static IEnumerable<object[]> GetModuleNames()
    {
        foreach (string moduleName in TestConstants.AllModules)
        {
            yield return [moduleName];
        }
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void DomainLayer_ShouldNotHaveForbiddenDependencies(string moduleName)
    {
        Assembly domainAssembly = GetModuleAssembly(moduleName, "Domain");

        string[] forbiddenDependencies =
        [
            $"Foundry.{moduleName}.Application",
            $"Foundry.{moduleName}.Infrastructure",
            $"Foundry.{moduleName}.Api",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Dapper",
            "Marten",
            "RabbitMQ.Client",
            "FluentValidation"
        ];

        foreach (string dependency in forbiddenDependencies)
        {
            TestResult result = Types.InAssembly(domainAssembly)
                .ShouldNot()
                .HaveDependencyOn(dependency)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Domain layer in {moduleName} module should not depend on {dependency}. " +
                $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
        }
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void ApplicationLayer_ShouldNotHaveForbiddenDependencies(string moduleName)
    {
        Assembly applicationAssembly = GetModuleAssembly(moduleName, "Application");

        List<string> forbiddenDependencies =
        [
            $"Foundry.{moduleName}.Infrastructure",
            $"Foundry.{moduleName}.Api",
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Dapper",
            "RabbitMQ.Client"
        ];

        // Marten is allowed in event-sourced modules' Application layer
        if (!TestConstants.EventSourcedModules.Contains(moduleName))
        {
            forbiddenDependencies.Add("Marten");
        }

        foreach (string dependency in forbiddenDependencies)
        {
            TestResult result = Types.InAssembly(applicationAssembly)
                .ShouldNot()
                .HaveDependencyOn(dependency)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Application layer in {moduleName} module should not depend on {dependency}. " +
                $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
        }
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void ApiLayer_ShouldNotDependOn_InfrastructureLayer(string moduleName)
    {
        Assembly apiAssembly = GetModuleAssembly(moduleName, "Api");

        TestResult result = Types.InAssembly(apiAssembly)
            .ShouldNot()
            .HaveDependencyOn($"Foundry.{moduleName}.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Api layer in {moduleName} module should not depend on Infrastructure layer. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void DomainEntities_ShouldBeSealed(string moduleName)
    {

        Assembly domainAssembly = GetModuleAssembly(moduleName, "Domain");

        TestResult result = Types.InAssembly(domainAssembly)
            .That()
            .ResideInNamespace($"Foundry.{moduleName}.Domain.Entities")
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain entities in {moduleName} module should be sealed to prevent inheritance. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void CommandsAndQueries_ShouldBeSealed(string moduleName)
    {
        Assembly applicationAssembly = GetModuleAssembly(moduleName, "Application");

        TestResult commandResult = Types.InAssembly(applicationAssembly)
            .That()
            .HaveNameEndingWith("Command")
            .Should()
            .BeSealed()
            .GetResult();

        commandResult.IsSuccessful.Should().BeTrue(
            $"Commands in {moduleName} module should be sealed. " +
            $"Failing types: {string.Join(", ", commandResult.FailingTypeNames ?? Array.Empty<string>())}");

        TestResult queryResult = Types.InAssembly(applicationAssembly)
            .That()
            .HaveNameEndingWith("Query")
            .And()
            .ResideInNamespaceStartingWith($"Foundry.{moduleName}.Application.Queries")
            .Should()
            .BeSealed()
            .GetResult();

        queryResult.IsSuccessful.Should().BeTrue(
            $"Queries in {moduleName} module should be sealed. " +
            $"Failing types: {string.Join(", ", queryResult.FailingTypeNames ?? Array.Empty<string>())}");
    }

    private static Assembly GetModuleAssembly(string moduleName, string layer)
    {
        string assemblyName = $"Foundry.{moduleName}.{layer}";
        return Assembly.Load(assemblyName);
    }
}
