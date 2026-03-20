namespace Wallow.Inquiries.Api.Contracts;

public sealed record InquiryResponse(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    string? Company,
    string? SubmitterId,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);
