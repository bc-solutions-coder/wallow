namespace Wallow.Shared.Contracts.Billing;

public interface IPaymentReportService
{
    Task<IReadOnlyList<PaymentReportRow>> GetPaymentsAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
}

public sealed record PaymentReportRow(
    Guid PaymentId,
    string InvoiceNumber,
    decimal Amount,
    string Currency,
    string Method,
    string Status,
    DateTime? PaymentDate);
