namespace Foundry.Inquiries.Api.Contracts;

public sealed record SubmitInquiryRequest(
    string Name,
    string Email,
    string? Company,
    string? Phone,
    string Subject,
    string Message);
