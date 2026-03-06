namespace Foundry.Inquiries.Api.Contracts;

public sealed record InquiryResponse(
    Guid Id,
    string Name,
    string Email,
    string? Company,
    string? Phone,
    string Subject,
    string Message,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);
