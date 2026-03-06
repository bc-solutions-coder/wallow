namespace Foundry.Inquiries.Application.DTOs;

public sealed record InquiryDto(
    Guid Id,
    string Name,
    string Email,
    string? Company,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message,
    string Status,
    string SubmitterIpAddress,
    DateTimeOffset CreatedAt);
