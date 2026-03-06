using Foundry.Inquiries.Application.DTOs;
using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Application.Mappings;
using Foundry.Inquiries.Domain.Entities;
using Foundry.Inquiries.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Inquiries.Application.Commands.UpdateInquiryStatus;

public static class UpdateInquiryStatusHandler
{
    public static async Task<Result<InquiryDto>> HandleAsync(
        UpdateInquiryStatusCommand command,
        IInquiryRepository inquiryRepository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        InquiryId inquiryId = InquiryId.Create(command.InquiryId);
        Inquiry? inquiry = await inquiryRepository.GetByIdAsync(inquiryId, cancellationToken);

        if (inquiry is null)
        {
            return Result.Failure<InquiryDto>(Error.NotFound("Inquiry", command.InquiryId));
        }

        inquiry.TransitionTo(command.NewStatus, timeProvider);
        await inquiryRepository.UpdateAsync(inquiry, cancellationToken);

        return Result.Success(inquiry.ToDto());
    }
}
