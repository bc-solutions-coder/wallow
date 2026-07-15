using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NetArchTest.Rules;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.MultiTenancy;

#pragma warning disable CA1024 // MemberData source methods cannot be properties
#pragma warning disable CA1310 // String comparison in LINQ lambdas over type names is culture-safe
#pragma warning disable CA1309 // String comparison in LINQ lambdas over type names is culture-safe
#pragma warning disable CA1860 // LINQ .Any() with predicate cannot use Count

namespace Wallow.Architecture.Tests;

public class MultiTenancyArchitectureTests
{
    private static readonly string[] _tenantAwareModules =
    [
        "Branding",
        "Notifications",
        "Announcements",
        "Inquiries"
    ];

    public static IEnumerable<object[]> GetTenantAwareModuleNames()
    {
        foreach (string moduleName in _tenantAwareModules)
        {
            yield return [moduleName];
        }
    }

    [Theory]
    [MemberData(nameof(GetTenantAwareModuleNames))]
    public void TenantAwareEntities_ShouldFollowMultiTenancyConventions(string moduleName)
    {
        Assembly domainAssembly = GetModuleAssembly(moduleName, "Domain");

        // At least some entities should implement ITenantScoped
        List<Type> entities = Types.InAssembly(domainAssembly)
            .That()
            .Inherit(typeof(Entity<>))
            .Or()
            .Inherit(typeof(AggregateRoot<>))
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsGenericType)
            .ToList();

        if (!entities.Any())
        {
            return;
        }

        int tenantScopedCount = entities.Count(e => typeof(ITenantScoped).IsAssignableFrom(e));
        int entityCount = entities.Count;

        tenantScopedCount.Should().BeGreaterThan(0,
            $"At least some entities in {moduleName} module should implement ITenantScoped. " +
            $"Found {tenantScopedCount} tenant-scoped out of {entityCount} total entities.");

        // Tenant-scoped entities should have proper TenantId property
        List<Type> tenantScopedEntities = Types.InAssembly(domainAssembly)
            .That()
            .ImplementInterface(typeof(ITenantScoped))
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .ToList();

        foreach (Type entity in tenantScopedEntities)
        {
            PropertyInfo? tenantIdProperty = entity.GetProperty(
                nameof(ITenantScoped.TenantId),
                BindingFlags.Public | BindingFlags.Instance);

            tenantIdProperty.Should().NotBeNull(
                $"Entity {entity.Name} in {moduleName} module must have a TenantId property");

            tenantIdProperty.PropertyType.Name.Should().Be("TenantId",
                $"TenantId property on {entity.Name} should be of type TenantId");

            tenantIdProperty.CanWrite.Should().BeTrue(
                $"TenantId property on {entity.Name} should have a setter for interceptor");

            // Should not expose public SetTenantId methods
            List<MethodInfo> setTenantIdMethods = entity.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name.Contains("SetTenantId") ||
                            (m.Name.StartsWith("set_") && m.Name.Contains("TenantId")))
                .ToList();

            int publicSetterCount = setTenantIdMethods.Count(m => !m.Name.Equals("set_TenantId"));

            publicSetterCount.Should().Be(0,
                $"Entity {entity.Name} in {moduleName} should not expose public SetTenantId method");
        }
    }

    [Theory]
    [MemberData(nameof(GetTenantAwareModuleNames))]
    public void DbContext_ShouldSupportMultiTenancy(string moduleName)
    {
        Assembly infrastructureAssembly = GetModuleAssembly(moduleName, "Infrastructure");

        List<Type> dbContexts = Types.InAssembly(infrastructureAssembly)
            .That()
            .Inherit(typeof(DbContext))
            .GetTypes()
            .Where(t => !t.IsAbstract)
            .ToList();

        foreach (Type dbContext in dbContexts)
        {
            // Should inherit from TenantAwareDbContext<T> which provides SetTenant and query filters
            Type? baseType = dbContext.BaseType;
            bool inheritsTenantAwareDbContext = baseType is { IsGenericType: true }
                && baseType.GetGenericTypeDefinition().FullName == "Wallow.Shared.Infrastructure.Core.Persistence.TenantAwareDbContext`1";

            inheritsTenantAwareDbContext.Should().BeTrue(
                $"DbContext {dbContext.Name} in {moduleName} module should inherit from TenantAwareDbContext<T>");

            // Should override OnModelCreating for query filters
            MethodInfo? onModelCreatingMethod = dbContext.GetMethod(
                "OnModelCreating",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

            onModelCreatingMethod.Should().NotBeNull(
                $"DbContext {dbContext.Name} in {moduleName} should override OnModelCreating to configure query filters");

            MethodBody? methodBody = onModelCreatingMethod.GetMethodBody();
            methodBody.Should().NotBeNull(
                $"OnModelCreating in {dbContext.Name} should have a method body");
        }
    }

    [Theory]
    [MemberData(nameof(GetTenantAwareModuleNames))]
    public void Repositories_ShouldNotTakeTenantIdParameter(string moduleName)
    {

        Assembly infrastructureAssembly = GetModuleAssembly(moduleName, "Infrastructure");

        List<Type> repositories = Types.InAssembly(infrastructureAssembly)
            .That()
            .HaveNameEndingWith("Repository")
            .And()
            .DoNotHaveNameMatching(".*Interface$")
            .GetTypes()
            .Where(t => !t.IsInterface && !t.IsAbstract)
            .ToList();

        foreach (Type repository in repositories)
        {
            MethodInfo[] methods = repository.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (MethodInfo method in methods)
            {
                ParameterInfo[] parameters = method.GetParameters();
                bool hasTenantIdParameter = parameters.Any(p => p.ParameterType.Name == "TenantId");

                hasTenantIdParameter.Should().BeFalse(
                    $"Repository {repository.Name}.{method.Name} in {moduleName} should not take TenantId as parameter. " +
                    $"Tenant filtering should be handled by DbContext query filters.");
            }
        }
    }

    private static Assembly GetModuleAssembly(string moduleName, string layer)
    {
        string assemblyName = $"Wallow.{moduleName}.{layer}";
        return Assembly.Load(assemblyName);
    }
}
