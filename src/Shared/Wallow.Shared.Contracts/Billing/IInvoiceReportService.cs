namespace Wallow.Shared.Contracts.Billing;

public interface IInvoiceReportService
{
    Task<IReadOnlyList<InvoiceReportRow>> GetInvoicesAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
}

public sealed record InvoiceReportRow(
    string InvoiceNumber,
    string CustomerName,
    decimal Amount,
    string Currency,
    string Status,
    DateTime IssueDate,
    DateTime? DueDate);
