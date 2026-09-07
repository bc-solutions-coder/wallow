using System.Reflection;
using NetArchTest.Rules;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Architecture.Tests;

public class SharedContractsTests
{
    [Fact]
    public void SharedContracts_ShouldNotDependOn_AnyModule()
    {
        Assembly sharedContractsAssembly = typeof(UserRegisteredEvent).Assembly;

        foreach (string moduleName in TestConstants.AllModules)
        {
            TestResult result = Types.InAssembly(sharedContractsAssembly)
                .ShouldNot()
                .HaveDependencyOn($"Wallow.{moduleName}")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Shared.Contracts should not depend on {moduleName}. " +
                $"Shared.Contracts must be dependency-free to avoid coupling.");
        }
    }

    [Fact]
    public void SharedKernel_ShouldNotDependOn_AnyModule()
    {
        Assembly sharedKernelAssembly = typeof(AggregateRoot<>).Assembly;

        foreach (string moduleName in TestConstants.AllModules)
        {
            TestResult result = Types.InAssembly(sharedKernelAssembly)
                .ShouldNot()
                .HaveDependencyOn($"Wallow.{moduleName}")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Shared.Kernel should not depend on {moduleName}. " +
                $"Shared.Kernel must be dependency-free to avoid coupling.");
        }
    }

    [Fact]
    public void SharedKernel_ShouldNotDependOn_SharedContracts()
    {
        Assembly sharedKernelAssembly = typeof(AggregateRoot<>).Assembly;

        TestResult result = Types.InAssembly(sharedKernelAssembly)
            .ShouldNot()
            .HaveDependencyOn("Wallow.Shared.Contracts")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Shared.Kernel should not depend on Shared.Contracts. " +
            "Shared.Kernel is lower-level and should have minimal dependencies.");
    }

    [Fact]
    public void SharedContracts_ShouldNotDependOn_EntityFramework()
    {
        Assembly sharedContractsAssembly = typeof(UserRegisteredEvent).Assembly;

        TestResult result = Types.InAssembly(sharedContractsAssembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "Shared.Contracts should not depend on Entity Framework. " +
            "Contracts should be pure DTOs with no infrastructure dependencies.");
    }

    [Fact]
    public void SharedKernel_CanDependOn_EntityFrameworkForInfrastructureHelpers()
    {
        Assembly sharedKernelAssembly = typeof(AggregateRoot<>).Assembly;

        // Strongly typed ID converters are an allowed EF dependency in the kernel.
        TestResult result = Types.InAssembly(sharedKernelAssembly)
            .That()
            .ResideInNamespace("Wallow.Shared.Kernel.Identity")
            .And()
            .HaveNameEndingWith("Converter")
            .Should()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();


        result.Should().NotBeNull();
    }
}
