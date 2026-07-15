using Wallow.Inquiries.Application.DTOs;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Application.Mappings;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Inquiries.Application.Queries.GetInquiries;

public sealed class GetInquiriesHandler(IInquiryRepository inquiryRepository)
{
    public async Task<Result<IReadOnlyList<InquiryDto>>> Handle(
        GetInquiriesQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Inquiry> inquiries = query.Status is not null
            ? await inquiryRepository.GetByStatusAsync(query.Status.Value, cancellationToken)
            : await inquiryRepository.GetAllAsync(cancellationToken);

        List<InquiryDto> dtos = inquiries.Select(i => i.ToDto()).ToList();
        return Result.Success<IReadOnlyList<InquiryDto>>(dtos);
    }
}
