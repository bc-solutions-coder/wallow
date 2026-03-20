// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Billing.Events;

/// <summary>
/// Published when an invoice becomes overdue.
/// Consumers: Communications (send reminder, notify user)
/// </summary>
public sealed record InvoiceOverdueEvent : IntegrationEvent
{
    public required Guid InvoiceId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid UserId { get; init; }
    public required string UserEmail { get; init; }
    public required string InvoiceNumber { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTime DueDate { get; init; }
}
