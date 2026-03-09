// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace Foundry.Shared.Contracts.Billing.Events;

/// <summary>
/// Published when an invoice is created.
/// Consumers: Communications (send invoice, notify user)
/// </summary>
public sealed record InvoiceCreatedEvent : IntegrationEvent
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
