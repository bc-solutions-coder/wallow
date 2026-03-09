// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Foundry.Shared.Contracts.Communications.Announcements.Events;

/// <summary>
/// Published when an announcement is published.
/// Consumers: Communications (push notifications on important announcements)
/// </summary>
public sealed record AnnouncementPublishedEvent : IntegrationEvent
{
    public required Guid AnnouncementId { get; init; }
    public required Guid TenantId { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required string Type { get; init; }
    public required string Target { get; init; }
    public required string? TargetValue { get; init; }
    public required bool IsPinned { get; init; }
}
