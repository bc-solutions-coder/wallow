using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace Foundry.Configuration.Infrastructure.Services;

/// <summary>
/// Manages dynamic indexes for heavily-queried custom fields.
/// Call this periodically or when field definitions change.
/// </summary>
public sealed partial class CustomFieldIndexManager(ILogger<CustomFieldIndexManager> logger)
{
    // Only alphanumeric and underscore, must start with letter or underscore
    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]{0,62}$", RegexOptions.NonBacktracking)]
    private static partial Regex SafeIdentifierRegex();

    /// <summary>
    /// Creates an expression index for a specific custom field.
    /// Use sparingly - only for fields with high query volume.
    /// </summary>
    public async Task CreateExpressionIndexAsync(
        DbContext context,
        string tableName,
        string schemaName,
        string fieldKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(tableName, nameof(tableName));
        ValidateIdentifier(schemaName, nameof(schemaName));
        ValidateIdentifier(fieldKey, nameof(fieldKey));

        string indexName = $"ix_{tableName}_{fieldKey}";

        const string checkSql = """
            SELECT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = {0}
                AND tablename = {1}
                AND indexname = {2}
            );
            """;

        bool exists = await context.Database
            .SqlQueryRaw<bool>(
                checkSql,
                schemaName,
                tableName,
                indexName)
            .FirstOrDefaultAsync(cancellationToken);

        if (exists)
        {
            LogIndexAlreadyExists(logger, indexName);
            return;
        }

        // DDL statements don't support parameterized identifiers —
        // all values are validated against the strict identifier regex above.
        string createSql = $"""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS {indexName}
            ON {schemaName}.{tableName} ((custom_fields->>'{fieldKey}'))
            WHERE custom_fields->>'{fieldKey}' IS NOT NULL;
            """;

        LogCreatingExpressionIndex(logger, indexName, fieldKey);

        await context.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }

    /// <summary>
    /// Drops an expression index when a field is no longer heavily queried.
    /// </summary>
    public async Task DropExpressionIndexAsync(
        DbContext context,
        string tableName,
        string schemaName,
        string fieldKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(tableName, nameof(tableName));
        ValidateIdentifier(schemaName, nameof(schemaName));
        ValidateIdentifier(fieldKey, nameof(fieldKey));

        string indexName = $"ix_{tableName}_{fieldKey}";

        // DDL doesn't support parameterized identifiers — values validated above.
        string dropSql = $"DROP INDEX CONCURRENTLY IF EXISTS {schemaName}.{indexName};";

        LogDroppingExpressionIndex(logger, indexName);

        await context.Database.ExecuteSqlRawAsync(dropSql, cancellationToken);
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (!SafeIdentifierRegex().IsMatch(value))
        {
            throw new ArgumentException(
                $"Invalid SQL identifier: '{value}'. Only letters, digits, and underscores are allowed.",
                parameterName);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Index {IndexName} already exists")]
    private static partial void LogIndexAlreadyExists(ILogger logger, string indexName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Creating expression index {IndexName} for field {FieldKey}")]
    private static partial void LogCreatingExpressionIndex(ILogger logger, string indexName, string fieldKey);

    [LoggerMessage(Level = LogLevel.Information, Message = "Dropping expression index {IndexName}")]
    private static partial void LogDroppingExpressionIndex(ILogger logger, string indexName);
}
