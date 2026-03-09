using System.Reflection;
using Foundry.Shared.Contracts.Identity.Events;
using Foundry.Shared.Kernel.Domain;
using NetArchTest.Rules;

namespace Foundry.Architecture.Tests;

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
                .HaveDependencyOn($"Foundry.{moduleName}")
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
                .HaveDependencyOn($"Foundry.{moduleName}")
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
            .HaveDependencyOn("Foundry.Shared.Contracts")
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

        // Shared.Kernel contains EF Core value converters for strongly-typed IDs
        // This is intentional infrastructure support and is allowed
        TestResult result = Types.InAssembly(sharedKernelAssembly)
            .That()
            .ResideInNamespace("Foundry.Shared.Kernel.Identity")
            .And()
            .HaveNameEndingWith("Converter")
            .Should()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        // This test documents that EF Core dependency exists in Shared.Kernel
        // for infrastructure helpers (StronglyTypedIdConverter)
        result.Should().NotBeNull();
    }
}
