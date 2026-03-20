using System.Reflection;
using FluentValidation;
using NetArchTest.Rules;

#pragma warning disable CA1024 // MemberData source methods cannot be properties
#pragma warning disable CA1310 // String comparison in LINQ lambdas over type names is culture-safe
#pragma warning disable CA1860 // LINQ .Any() with predicate cannot use Count

namespace Wallow.Architecture.Tests;

public class CqrsConventionTests
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
    public void CommandConventions_ShouldBeFollowed(string moduleName)
    {
        Assembly applicationAssembly = GetModuleAssembly(moduleName, "Application");

        // Commands should end with "Command" and reside in Commands namespace
        List<Type> commandClasses = Types.InAssembly(applicationAssembly)
            .That()
            .ResideInNamespace($"Wallow.{moduleName}.Application.Commands")
            .And()
            .AreNotAbstract()
            .And()
            .AreNotInterfaces()
            .GetTypes()
            .Where(t => !t.Name.EndsWith("Handler")
                && !t.Name.EndsWith("Validator")
                && !t.Name.Contains("Result")
                && !t.IsNested)
            .ToList();

        foreach (Type command in commandClasses)
        {
            command.Name.Should().EndWith("Command",
                $"Command class {command.FullName} should end with 'Command' suffix");
        }

        // Commands named *Command should reside in a namespace containing .Commands
        List<Type> commandTypes = Types.InAssembly(applicationAssembly)
            .That()
            .HaveNameEndingWith("Command")
            .And()
            .DoNotHaveNameEndingWith("CommandValidator")
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => !t.IsNested)
            .ToList();

        if (commandTypes.Any())
        {
            List<Type> violatingCommands = commandTypes
                .Where(t => t.Namespace != null && !t.Namespace.Contains(".Commands"))
                .ToList();

            violatingCommands.Should().BeEmpty(
                $"All command types in {moduleName} module should reside in a Commands namespace. " +
                $"Failing types: {string.Join(", ", violatingCommands.Select(t => t.FullName))}");
        }

        // Command handlers should end with "Handler" and have matching command
        List<Type> handlers = Types.InAssembly(applicationAssembly)
            .That()
            .ResideInNamespace($"Wallow.{moduleName}.Application.Commands")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => !t.Name.EndsWith("Command")
                && !t.Name.EndsWith("Validator")
                && !t.Name.Contains("Result")
                && !t.IsNested)
            .ToList();

        foreach (Type handler in handlers)
        {
            handler.Name.Should().EndWith("Handler",
                $"Handler class {handler.FullName} should end with 'Handler' suffix");
        }

        if (moduleName != "Storage" && handlers.Any())
        {
            HashSet<string> commands = Types.InAssembly(applicationAssembly)
                .That()
                .ResideInNamespace($"Wallow.{moduleName}.Application.Commands")
                .And()
                .AreNotAbstract()
                .GetTypes()
                .Where(t => !t.IsNested && (t.Name.EndsWith("Command") || !t.Name.EndsWith("Handler")))
                .Select(t => t.Name.Replace("Command", ""))
                .ToHashSet();

            foreach (Type handler in handlers.Where(h => h.Name.EndsWith("Handler")))
            {
                string handlerBaseName = handler.Name.Replace("Handler", "");
                commands.Should().Contain(handlerBaseName,
                    $"Handler {handler.FullName} should have a corresponding command");
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void QueryConventions_ShouldBeFollowed(string moduleName)
    {
        Assembly applicationAssembly = GetModuleAssembly(moduleName, "Application");

        // Queries should end with "Query"
        List<Type> queryClasses = Types.InAssembly(applicationAssembly)
            .That()
            .ResideInNamespace($"Wallow.{moduleName}.Application.Queries")
            .And()
            .AreNotAbstract()
            .And()
            .AreNotInterfaces()
            .GetTypes()
            .Where(t => !t.Name.EndsWith("Handler")
                && !t.Name.EndsWith("Validator")
                && !t.Name.Contains("Result")
                && !t.IsNested)
            .ToList();

        foreach (Type query in queryClasses)
        {
            query.Name.Should().EndWith("Query",
                $"Query class {query.FullName} should end with 'Query' suffix");
        }

        // Queries named *Query should reside in a namespace containing .Queries
        List<Type> queryTypes = Types.InAssembly(applicationAssembly)
            .That()
            .HaveNameEndingWith("Query")
            .And()
            .DoNotHaveNameEndingWith("QueryValidator")
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => !t.IsNested)
            .ToList();

        if (queryTypes.Any())
        {
            List<Type> violatingQueries = queryTypes
                .Where(t => t.Namespace != null && !t.Namespace.Contains(".Queries"))
                .ToList();

            violatingQueries.Should().BeEmpty(
                $"All query types in {moduleName} module should reside in a Queries namespace. " +
                $"Failing types: {string.Join(", ", violatingQueries.Select(t => t.FullName))}");
        }

        // Query handlers should end with "Handler" and have matching query
        List<Type> handlers = Types.InAssembly(applicationAssembly)
            .That()
            .ResideInNamespace($"Wallow.{moduleName}.Application.Queries")
            .And()
            .HaveNameEndingWith("Handler")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => !t.IsNested)
            .ToList();

        if (handlers.Any())
        {
            HashSet<string> queries = Types.InAssembly(applicationAssembly)
                .That()
                .ResideInNamespace($"Wallow.{moduleName}.Application.Queries")
                .And()
                .AreNotAbstract()
                .GetTypes()
                .Where(t => !t.IsNested && (t.Name.EndsWith("Query") || !t.Name.EndsWith("Handler")))
                .Select(t => t.Name.Replace("Query", ""))
                .ToHashSet();

            foreach (Type handler in handlers)
            {
                string handlerBaseName = handler.Name.Replace("Handler", "");
                queries.Should().Contain(handlerBaseName,
                    $"Handler {handler.FullName} should have a corresponding query");
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetModuleNames))]
    public void Validators_ShouldEndWithValidator(string moduleName)
    {
        Assembly applicationAssembly = GetModuleAssembly(moduleName, "Application");

        IEnumerable<Type> validatorTypes = Types.InAssembly(applicationAssembly)
            .That()
            .Inherit(typeof(AbstractValidator<>))
            .GetTypes();

        foreach (Type validator in validatorTypes)
        {
            validator.Name.Should().EndWith("Validator",
                $"Validator class {validator.FullName} should end with 'Validator' suffix");
        }
    }

    private static Assembly GetModuleAssembly(string moduleName, string layer)
    {
        string assemblyName = $"Wallow.{moduleName}.{layer}";
        return Assembly.Load(assemblyName);
    }
}
