// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Requests approval to join an organization. The pending membership is the request.
/// Notifications builds the review URL from its configuration and emails each recipient.
/// </summary>
public sealed record AccessRequestedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required string OrganizationName { get; init; }
    public required Guid RequesterUserId { get; init; }
    public required string RequesterEmail { get; init; }
    public required string RequesterName { get; init; }
    public required IReadOnlyList<string> RecipientEmails { get; init; }
}
