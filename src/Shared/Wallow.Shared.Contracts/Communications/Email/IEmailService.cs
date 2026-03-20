namespace Wallow.Shared.Contracts.Communications.Email;

public interface IEmailService
{
    Task SendAsync(string to, string? from, string subject, string body, CancellationToken cancellationToken = default);

    Task SendWithAttachmentAsync(
        string to,
        string? from,
        string subject,
        string body,
        byte[] attachment,
        string attachmentName,
        string attachmentContentType = "application/octet-stream",
        CancellationToken cancellationToken = default);
}
