namespace Wallow.Shared.Kernel.CustomFields;

/// <summary>
/// Registry of entity types that support custom fields.
/// </summary>
public static class CustomFieldRegistry
{
    private static readonly Dictionary<string, EntityTypeInfo> _entityTypes = new()
    {
    };

    public static IReadOnlyList<EntityTypeInfo> GetSupportedEntityTypes()
        => _entityTypes.Values.ToList();

    public static bool IsSupported(string entityType)
        => _entityTypes.ContainsKey(entityType);

    public static EntityTypeInfo? GetEntityType(string entityType)
        => _entityTypes.GetValueOrDefault(entityType);

    /// <summary>
    /// Registers or replaces an entity type during module initialization.
    /// </summary>
    public static void Register(string entityType, string module, string description)
        => _entityTypes[entityType] = new(entityType, module, description);
}

/// <summary>
/// Information about an entity type that supports custom fields.
/// </summary>
public sealed record EntityTypeInfo(string EntityType, string Module, string Description);
