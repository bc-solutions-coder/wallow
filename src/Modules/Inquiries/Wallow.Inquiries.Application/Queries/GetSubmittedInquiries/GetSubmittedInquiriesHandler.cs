using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Application.Mappings;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Application.Queries.GetSubmittedInquiries;

public sealed class GetSubmittedInquiriesHandler(IInquiryRepository inquiryRepository)
{
    public async Task<Result<IReadOnlyList<InquiryDto>>> Handle(
        GetSubmittedInquiriesQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Inquiry> inquiries = await inquiryRepository.GetBySubmitterAsync(
            query.SubmitterId, cancellationToken);

        List<InquiryDto> dtos = inquiries.Select(i => i.ToDto()).ToList();
        return Result.Success<IReadOnlyList<InquiryDto>>(dtos);
    }
}
