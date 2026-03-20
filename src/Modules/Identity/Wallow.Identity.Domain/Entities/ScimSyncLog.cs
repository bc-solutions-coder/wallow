using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Domain.Events;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Identity.Domain.Entities;

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

    // ReSharper disable once UnusedMember.Local
    private ScimSyncLog() { } // EF Core

    private ScimSyncLog(
        TenantId tenantId,
        ScimOperation operation,
        ScimResourceType resourceType,
        string externalId,
        string? internalId,
        bool success,
        string? errorMessage,
        string? requestBody,
        TimeProvider timeProvider)
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
        Timestamp = timeProvider.GetUtcNow().UtcDateTime;
    }

    public static ScimSyncLog Create(
        TenantId tenantId,
        ScimOperation operation,
        ScimResourceType resourceType,
        string externalId,
        string? internalId,
        bool success,
        TimeProvider timeProvider,
        string? errorMessage = null,
        string? requestBody = null)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new BusinessRuleException(
                "Identity.ExternalIdRequired",
                "SCIM external ID is required");
        }

        ScimSyncLog log = new(
            tenantId,
            operation,
            resourceType,
            externalId,
            internalId,
            success,
            errorMessage,
            requestBody,
            timeProvider);

        log.RaiseDomainEvent(new ScimSyncCompletedEvent(
            tenantId.Value,
            operation.ToString(),
            resourceType.ToString(),
            success,
            errorMessage));

        return log;
    }
}
