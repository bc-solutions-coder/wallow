using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Shared.Contracts.Communications.Email;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed class EmailProviderAdapter(IEmailProvider emailProvider) : IEmailService
{
    public async Task SendAsync(string to, string? from, string subject, string body, CancellationToken cancellationToken = default)
    {
        EmailDeliveryRequest request = new(to, from, subject, body);
        EmailDeliveryResult result = await emailProvider.SendAsync(request, cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to send email to {to}: {result.ErrorMessage}");
        }
    }

    public async Task SendWithAttachmentAsync(
        string to,
        string? from,
        string subject,
        string body,
        byte[] attachment,
        string attachmentName,
        string attachmentContentType = "application/octet-stream",
        CancellationToken cancellationToken = default)
    {
        EmailDeliveryRequest request = new(to, from, subject, body, new ReadOnlyMemory<byte>(attachment), attachmentName, attachmentContentType);
        EmailDeliveryResult result = await emailProvider.SendAsync(request, cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to send email to {to}: {result.ErrorMessage}");
        }
    }
}
