namespace Foundry.Inquiries.Api.Contracts;

public sealed record AddInquiryCommentRequest(
    string Content,
    bool IsInternal);
