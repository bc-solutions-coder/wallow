using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Application.Mappings;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Application.Commands.UpdateInquiryStatus;

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
        await inquiryRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(inquiry.ToDto());
    }
}
