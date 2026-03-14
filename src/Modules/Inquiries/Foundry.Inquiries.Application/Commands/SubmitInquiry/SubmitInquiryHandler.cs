using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Mappings;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Application.Commands.SubmitInquiry;

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

        return Result.Success(inquiry.ToDto());
    }
}
