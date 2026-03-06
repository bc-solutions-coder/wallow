namespace Foundry.Inquiries.Application.Commands.SubmitInquiry;

public sealed record SubmitInquiryCommand(
    string Name,
    string Email,
    string? Company,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message,
    string SubmitterIpAddress,
    string? HoneypotField);
