namespace Wallow.Shared.Kernel.CustomFields;

/// <summary>
/// Registry of entity types that support custom fields.
/// </summary>
public static class CustomFieldRegistry
{
    private static readonly Dictionary<string, EntityTypeInfo> _entityTypes = new()
    {
    };

    /// <summary>
    /// Gets all entity types that support custom fields.
    /// </summary>
    public static IReadOnlyList<EntityTypeInfo> GetSupportedEntityTypes()
        => _entityTypes.Values.ToList();

    /// <summary>
    /// Checks if an entity type supports custom fields.
    /// </summary>
    public static bool IsSupported(string entityType)
        => _entityTypes.ContainsKey(entityType);

    /// <summary>
    /// Gets info for a specific entity type.
    /// </summary>
    public static EntityTypeInfo? GetEntityType(string entityType)
        => _entityTypes.GetValueOrDefault(entityType);

    /// <summary>
    /// Registers a new entity type (call from module initialization).
    /// </summary>
    public static void Register(string entityType, string module, string description)
        => _entityTypes[entityType] = new(entityType, module, description);
}

/// <summary>
/// Information about an entity type that supports custom fields.
/// </summary>
public sealed record EntityTypeInfo(string EntityType, string Module, string Description);
