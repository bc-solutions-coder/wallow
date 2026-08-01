// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Identity.Events;

/// <summary>
/// Published when someone asks to join an organization whose enrollment policy is
/// <c>RequestApproval</c>. The pending membership IS the request, so there is no separate request id.
/// Consumers: Notifications (access-request email to the recipients).
/// </summary>
/// <remarks>
/// Carries the identifiers a review link needs rather than the link itself: composing the URL is the
/// consuming handler's job, from its own service-url configuration, exactly as
/// <see cref="InvitationCreatedEvent"/> carries a token and not a URL.
/// <para>
/// <c>RecipientEmails</c> is a list from the outset so that routing an organization's access requests
/// to a role or a group later stays a change inside Identity's resolver, with no contract, handler or
/// template to revisit.
/// </para>
/// </remarks>
public sealed record AccessRequestedEvent : IntegrationEvent
{
    public required Guid TenantId { get; init; }
    public required string OrganizationName { get; init; }
    public required Guid RequesterUserId { get; init; }
    public required string RequesterEmail { get; init; }
    public required string RequesterName { get; init; }
    public required IReadOnlyList<string> RecipientEmails { get; init; }
}
