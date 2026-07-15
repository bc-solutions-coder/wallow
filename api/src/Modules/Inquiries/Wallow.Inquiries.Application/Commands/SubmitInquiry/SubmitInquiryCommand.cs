namespace Wallow.Inquiries.Application.Commands.SubmitInquiry;

public sealed record SubmitInquiryCommand(
    string Name,
    string Email,
    string Phone,
    string? Company,
    string? SubmitterId,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message);
