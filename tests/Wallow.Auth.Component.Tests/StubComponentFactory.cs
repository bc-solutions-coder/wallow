using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Wallow.Auth.Component.Tests;

public sealed class StubComponentFactory : IComponentFactory
{
    private static readonly Assembly _stubAssembly = typeof(StubComponentFactory).Assembly;
    private static readonly Dictionary<Type, Type> _typeMap = BuildTypeMap();

    private static Dictionary<Type, Type> BuildTypeMap()
    {
        // Stubs live in the Stubs namespace. Map them to real BlazorBlueprint types by name.
        Dictionary<string, Type> stubsByShortName = _stubAssembly.GetTypes()
            .Where(t => t.Namespace == "Wallow.Auth.Component.Tests.Stubs" && typeof(IComponent).IsAssignableFrom(t))
            .ToDictionary(t => t.Name);

        Dictionary<Type, Type> map = new();
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly == _stubAssembly)
            {
                continue;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch
            {
                continue;
            }

            foreach (Type type in types)
            {
                if (type.Namespace == "BlazorBlueprint.Components" &&
                    stubsByShortName.TryGetValue(type.Name, out Type? stubType))
                {
                    map[type] = stubType;
                }
            }
        }

        return map;
    }

    public bool CanCreate(Type componentType) => _typeMap.ContainsKey(componentType);

    public IComponent Create(Type componentType)
    {
        Type stubType = _typeMap[componentType];
        return (IComponent)Activator.CreateInstance(stubType)!;
    }
}
