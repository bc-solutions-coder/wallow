using Foundry.Shared.Kernel.Identity;

namespace Foundry.Inquiries.Domain.Identity;

public readonly record struct InquiryCommentId(Guid Value) : IStronglyTypedId<InquiryCommentId>
{
    public static InquiryCommentId Create(Guid value) => new(value);
    public static InquiryCommentId New() => new(Guid.NewGuid());
}
