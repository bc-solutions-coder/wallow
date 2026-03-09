// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Foundry.Shared.Contracts.Billing.Events;

/// <summary>
/// Published when an invoice is marked as paid.
/// Consumers: Communications (send receipt, notify user)
/// </summary>
public sealed record InvoicePaidEvent : IntegrationEvent
{
    public required Guid InvoiceId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid PaymentId { get; init; }
    public required Guid UserId { get; init; }
    public required string InvoiceNumber { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateTime PaidAt { get; init; }
}
