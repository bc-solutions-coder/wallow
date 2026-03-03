using System.Reflection;
using Foundry.Shared.Contracts;

namespace Foundry.Shared.Infrastructure.AsyncApi;

public sealed record EventFlowInfo(
    Type EventType,
    string EventTypeName,
    string SourceModule,
    string ExchangeName,
    IReadOnlyList<ConsumerInfo> Consumers,
    bool SagaTrigger)
{
    public IReadOnlyList<string> ConsumerModules =>
        Consumers.Select(c => c.Module).Distinct().ToList();
}

public sealed record ConsumerInfo(
    string Module,
    string HandlerTypeName,
    string HandlerMethodName,
    bool IsSaga);

public sealed class EventFlowDiscovery
{
    private const string ContractsNamespacePrefix = "Foundry.Shared.Contracts.";
    private const string ModuleNamespacePrefix = "Foundry.";

    public IReadOnlyList<EventFlowInfo> Discover(IEnumerable<Assembly> assemblies)
    {
        List<Assembly> assemblyList = assemblies.ToList();

        List<Type> eventTypes = assemblyList
            .SelectMany(SafeGetTypes)
            .Where(t => typeof(IIntegrationEvent).IsAssignableFrom(t)
                && !t.IsAbstract && !t.IsInterface)
            .ToList();

        List<(MethodInfo Method, Type DeclaringType)> handlers = assemblyList
            .SelectMany(SafeGetTypes)
            .Where(t => t.IsClass)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name is "Handle" or "HandleAsync")
                .Select(m => (Method: m, DeclaringType: t)))
            .ToList();

        List<Type> sagaTypes = assemblyList
            .SelectMany(SafeGetTypes)
            .Where(IsSagaType)
            .ToList();

        List<EventFlowInfo> flows = new List<EventFlowInfo>();
        foreach (Type eventType in eventTypes)
        {
            string sourceModule = ExtractModuleFromContractsNamespace(eventType);
            string exchangeName = eventType.FullName ?? eventType.Name;

            List<ConsumerInfo> consumers = new List<ConsumerInfo>();

            foreach ((MethodInfo method, Type declaringType) in handlers)
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length > 0 && parameters[0].ParameterType == eventType)
                {
                    string module = ExtractModuleFromHandlerNamespace(declaringType);
                    consumers.Add(new ConsumerInfo(module, declaringType.Name, method.Name, IsSaga: false));
                }
            }

            bool isSagaTrigger = false;
            foreach (Type sagaType in sagaTypes)
            {
                foreach (MethodInfo startMethod in sagaType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "Start"))
                {
                    ParameterInfo[] parameters = startMethod.GetParameters();
                    if (parameters.Length > 0 && parameters[0].ParameterType == eventType)
                    {
                        string module = ExtractModuleFromHandlerNamespace(sagaType);
                        consumers.Add(new ConsumerInfo(module, sagaType.Name, "Start", IsSaga: true));
                        isSagaTrigger = true;
                    }
                }

                foreach (MethodInfo handleMethod in sagaType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name is "Handle" or "HandleAsync"))
                {
                    ParameterInfo[] parameters = handleMethod.GetParameters();
                    if (parameters.Length > 0 && parameters[0].ParameterType == eventType)
                    {
                        string module = ExtractModuleFromHandlerNamespace(sagaType);
                        consumers.Add(new ConsumerInfo(module, sagaType.Name, handleMethod.Name, IsSaga: true));
                    }
                }
            }

            flows.Add(new EventFlowInfo(
                eventType,
                eventType.Name,
                sourceModule,
                exchangeName,
                consumers,
                isSagaTrigger));
        }

        return flows.OrderBy(f => f.SourceModule).ThenBy(f => f.EventTypeName).ToList();
    }

    private static string ExtractModuleFromContractsNamespace(Type type)
    {
        string? ns = type.Namespace;
        if (ns is null || !ns.StartsWith(ContractsNamespacePrefix, StringComparison.Ordinal))
        {
            return "Unknown";
        }

        string remainder = ns[ContractsNamespacePrefix.Length..];
        int dotIndex = remainder.IndexOf('.', StringComparison.Ordinal);
        return dotIndex > 0 ? remainder[..dotIndex] : remainder;
    }

    private static string ExtractModuleFromHandlerNamespace(Type type)
    {
        string? ns = type.Namespace;
        if (ns is null || !ns.StartsWith(ModuleNamespacePrefix, StringComparison.Ordinal))
        {
            return "Unknown";
        }

        string remainder = ns[ModuleNamespacePrefix.Length..];

        if (remainder.StartsWith("Shared.", StringComparison.Ordinal))
        {
            return "Shared";
        }

        int dotIndex = remainder.IndexOf('.', StringComparison.Ordinal);
        return dotIndex > 0 ? remainder[..dotIndex] : remainder;
    }

    private static bool IsSagaType(Type type) =>
        !type.IsAbstract && type.IsClass &&
        GetBaseTypes(type).Any(bt => bt.Name == "Saga");

    private static IEnumerable<Type> GetBaseTypes(Type type)
    {
        Type? current = type.BaseType;
        while (current is not null)
        {
            yield return current;
            current = current.BaseType;
        }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.OfType<Type>(); }
    }
}
