namespace Wallow.Billing.Api.Contracts.Payments;

public sealed record ProcessPaymentRequest(
    decimal Amount,
    string Currency,
    string PaymentMethod);
