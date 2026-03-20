// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Wallow.Shared.Contracts.Billing.Events;

/// <summary>
/// Published when a payment is received.
/// Consumers: Communications (receipt, confirmation)
/// </summary>
public sealed record PaymentReceivedEvent : IntegrationEvent
{
    public required Guid PaymentId { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid InvoiceId { get; init; }
    public required Guid UserId { get; init; }
    public required string UserEmail { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required string PaymentMethod { get; init; }
    public required DateTime PaidAt { get; init; }
}
