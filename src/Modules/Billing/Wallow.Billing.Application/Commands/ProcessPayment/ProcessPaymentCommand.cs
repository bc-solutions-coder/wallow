namespace Wallow.Billing.Application.Commands.ProcessPayment;

public sealed record ProcessPaymentCommand(
    Guid InvoiceId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    Dictionary<string, object>? CustomFields = null);
