namespace Wallow.Shared.Contracts.Identity.Events;

public sealed record OrganizationSettingsUpdatedEvent : IntegrationEvent
{
    public required Guid OrganizationId { get; init; }
    public required Guid TenantId { get; init; }
    public required bool RequireMfa { get; init; }
    public required bool AllowPasswordlessLogin { get; init; }
    public required int MfaGracePeriodDays { get; init; }
}
