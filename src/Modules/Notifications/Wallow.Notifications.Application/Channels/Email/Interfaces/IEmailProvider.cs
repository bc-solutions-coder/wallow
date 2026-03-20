namespace Wallow.Notifications.Application.Channels.Email.Interfaces;

public record EmailDeliveryRequest(
    string To,
    string? From,
    string Subject,
    string Body,
    ReadOnlyMemory<byte>? Attachment = null,
    string? AttachmentName = null,
    string AttachmentContentType = "application/octet-stream");

public readonly record struct EmailDeliveryResult(bool Success, string? ErrorMessage);

public interface IEmailProvider
{
    Task<EmailDeliveryResult> SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken = default);
}
