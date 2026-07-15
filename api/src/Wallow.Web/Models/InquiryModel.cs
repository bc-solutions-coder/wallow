namespace Wallow.Web.Models;

public sealed record InquiryModel(
    string Name,
    string Email,
    string Phone,
    string? Company,
    string ProjectType,
    string BudgetRange,
    string Timeline,
    string Message);
