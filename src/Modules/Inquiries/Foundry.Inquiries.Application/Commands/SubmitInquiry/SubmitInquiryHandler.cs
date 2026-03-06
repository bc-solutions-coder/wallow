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
        IRateLimitService rateLimitService,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // Honeypot field filled = bot submission; return success silently to not reveal the trap
        if (!string.IsNullOrWhiteSpace(command.HoneypotField))
        {
            return Result.Success(new InquiryDto(
                Guid.NewGuid(),
                command.Name,
                command.Email,
                command.Company,
                command.ProjectType,
                command.BudgetRange,
                command.Timeline,
                command.Message,
                "New",
                command.SubmitterIpAddress,
                timeProvider.GetUtcNow()));
        }

        bool isAllowed = await rateLimitService.IsAllowedAsync(command.SubmitterIpAddress, cancellationToken);
        if (!isAllowed)
        {
            return Result.Failure<InquiryDto>(
                Error.Conflict("Too many inquiries submitted. Please try again later."));
        }

        Inquiry inquiry = Inquiry.Create(
            command.Name,
            command.Email,
            command.Company,
            command.ProjectType,
            command.BudgetRange,
            command.Timeline,
            command.Message,
            command.SubmitterIpAddress,
            timeProvider);

        await inquiryRepository.AddAsync(inquiry, cancellationToken);

        return Result.Success(inquiry.ToDto());
    }
}
