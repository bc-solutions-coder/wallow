using Foundry.Identity.Domain.Enums;
using Foundry.Identity.Domain.Events;
using Foundry.Identity.Domain.Identity;
using Foundry.Shared.Kernel.Domain;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;

namespace Foundry.Identity.Domain.Entities;

public sealed class ScimSyncLog : AggregateRoot<ScimSyncLogId>, ITenantScoped
{
    public TenantId TenantId { get; init; }
    public ScimOperation Operation { get; private set; }
    public ScimResourceType ResourceType { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;
    public string? InternalId { get; private set; }
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? RequestBody { get; private set; }
    public DateTime Timestamp { get; private set; }

    private ScimSyncLog() { }

    private ScimSyncLog(
        TenantId tenantId,
        ScimOperation operation,
        ScimResourceType resourceType,
        string externalId,
        string? internalId,
        bool success,
        string? errorMessage,
        string? requestBody)
    {
        Id = ScimSyncLogId.New();
        TenantId = tenantId;
        Operation = operation;
        ResourceType = resourceType;
        ExternalId = externalId;
        InternalId = internalId;
        Success = success;
        ErrorMessage = errorMessage;
        RequestBody = requestBody;
        Timestamp = DateTime.UtcNow;
    }

    public static ScimSyncLog Create(
        TenantId tenantId,
        ScimOperation operation,
        ScimResourceType resourceType,
        string externalId,
        string? internalId,
        bool success,
        string? errorMessage = null,
        string? requestBody = null)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new BusinessRuleException(
                "Identity.ExternalIdRequired",
                "SCIM external ID is required");
        }

        ScimSyncLog log = new ScimSyncLog(
            tenantId,
            operation,
            resourceType,
            externalId,
            internalId,
            success,
            errorMessage,
            requestBody);

        log.RaiseDomainEvent(new ScimSyncCompletedEvent(
            tenantId.Value,
            operation.ToString(),
            resourceType.ToString(),
            success,
            errorMessage));

        return log;
    }
}
