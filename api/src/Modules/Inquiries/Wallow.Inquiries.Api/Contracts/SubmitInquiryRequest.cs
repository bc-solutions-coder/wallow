namespace Wallow.Inquiries.Api.Contracts;

public sealed record SubmitInquiryRequest(
    string Name,
    string Email,
    string Phone,
    string? Company,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message);
