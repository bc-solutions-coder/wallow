using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Application.Mappings;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Application.Commands.SubmitInquiry;

public static class SubmitInquiryHandler
{
    public static async Task<Result<InquiryDto>> HandleAsync(
        SubmitInquiryCommand command,
        IInquiryRepository inquiryRepository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        Inquiry inquiry = Inquiry.Create(
            command.Name,
            command.Email,
            command.Phone,
            command.Company,
            command.SubmitterId,
            command.ProjectType,
            command.BudgetRange,
            command.Timeline,
            command.Message,
            string.Empty,
            timeProvider);

        await inquiryRepository.AddAsync(inquiry, cancellationToken);
        await inquiryRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(inquiry.ToDto());
    }
}
