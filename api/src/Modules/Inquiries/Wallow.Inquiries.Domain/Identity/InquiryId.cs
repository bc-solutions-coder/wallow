using Wallow.Shared.Kernel.Identity;

namespace Wallow.Inquiries.Domain.Identity;

public readonly record struct InquiryId(Guid Value) : IStronglyTypedId<InquiryId>
{
    public static InquiryId Create(Guid value) => new(value);
    public static InquiryId New() => new(Guid.NewGuid());
}
