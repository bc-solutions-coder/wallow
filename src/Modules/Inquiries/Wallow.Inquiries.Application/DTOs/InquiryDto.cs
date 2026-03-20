namespace Wallow.Inquiries.Application.DTOs;

public sealed record InquiryDto(
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
    string SubmitterIpAddress,
    DateTimeOffset CreatedAt);
