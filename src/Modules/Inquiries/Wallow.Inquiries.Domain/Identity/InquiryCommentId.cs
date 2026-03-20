using Wallow.Shared.Kernel.Identity;

namespace Wallow.Inquiries.Domain.Identity;

public readonly record struct InquiryCommentId(Guid Value) : IStronglyTypedId<InquiryCommentId>
{
    public static InquiryCommentId Create(Guid value) => new(value);
    public static InquiryCommentId New() => new(Guid.NewGuid());
}
