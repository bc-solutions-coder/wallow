// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Foundry.Shared.Contracts.Announcements.Events;

/// <summary>
/// Published when an announcement is published.
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
    public required IReadOnlyList<Guid> TargetUserIds { get; init; }
}
