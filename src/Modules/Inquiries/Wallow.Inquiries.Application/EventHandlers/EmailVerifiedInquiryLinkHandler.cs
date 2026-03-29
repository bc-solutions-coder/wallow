using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Shared.Contracts.Identity.Events;

namespace Wallow.Inquiries.Application.EventHandlers;

public static class EmailVerifiedInquiryLinkHandler
{
    public static async Task HandleAsync(
        EmailVerifiedEvent message,
        IInquiryRepository repository,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        IReadOnlyList<Inquiry> unlinkedInquiries = await repository.GetUnlinkedByEmailAsync(
            message.Email, ct);

        if (unlinkedInquiries.Count == 0)
        {
            return;
        }

        string userId = message.UserId.ToString();

        foreach (Inquiry inquiry in unlinkedInquiries)
        {
            inquiry.LinkToSubmitter(userId, timeProvider);
        }

        await repository.SaveChangesAsync(ct);
    }
}
