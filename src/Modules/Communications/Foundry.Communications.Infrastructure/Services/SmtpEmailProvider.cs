using System.Diagnostics;
using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Application.Channels.Email.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Polly;
using Polly.Registry;

namespace Foundry.Communications.Infrastructure.Services;

public sealed partial class SmtpEmailProvider(
    SmtpConnectionPool connectionPool,
    IOptions<SmtpSettings> settings,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<SmtpEmailProvider> logger) : IEmailProvider
{
    private readonly SmtpSettings _settings = settings.Value;
    private readonly ResiliencePipeline _smtpPipeline = pipelineProvider.GetPipeline("smtp");

    public async Task<EmailDeliveryResult> SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        using MimeMessage message = request.Attachment.HasValue
            ? BuildMessageWithAttachment(request)
            : BuildMessage(request);

        try
        {
            await SendWithPipelineAsync(message, cancellationToken);
            return new EmailDeliveryResult(true, null);
        }
        catch (Exception ex)
        {
            return new EmailDeliveryResult(false, ex.Message);
        }
    }

    private MimeMessage BuildMessage(EmailDeliveryRequest request)
    {
        MimeMessage message = new();

        message.From.Add(string.IsNullOrWhiteSpace(request.From)
            ? new MailboxAddress(_settings.DefaultFromName, _settings.DefaultFromAddress)
            : MailboxAddress.Parse(request.From));

        message.To.Add(MailboxAddress.Parse(request.To));
        message.Subject = request.Subject ?? string.Empty;

        message.Body = new TextPart("html")
        {
            Text = request.Body
        };

        return message;
    }

    private MimeMessage BuildMessageWithAttachment(EmailDeliveryRequest request)
    {
        const int maxAttachmentSizeBytes = 10 * 1024 * 1024; // 10MB

        ReadOnlyMemory<byte> attachment = request.Attachment!.Value;

        if (attachment.Length > maxAttachmentSizeBytes)
        {
            throw new InvalidOperationException(
                $"Attachment size ({attachment.Length / 1024 / 1024}MB) exceeds maximum allowed size (10MB)");
        }

        MimeMessage message = new();

        message.From.Add(string.IsNullOrWhiteSpace(request.From)
            ? new MailboxAddress(_settings.DefaultFromName, _settings.DefaultFromAddress)
            : MailboxAddress.Parse(request.From));

        message.To.Add(MailboxAddress.Parse(request.To));
        message.Subject = request.Subject ?? string.Empty;

        BodyBuilder builder = new()
        {
            HtmlBody = request.Body
        };

        builder.Attachments.Add(
            request.AttachmentName ?? "attachment",
            attachment.ToArray(),
            ContentType.Parse(request.AttachmentContentType));

        message.Body = builder.ToMessageBody();

        return message;
    }

    private async Task SendWithPipelineAsync(MimeMessage message, CancellationToken cancellationToken = default)
    {
        using Activity? activity = EmailModuleTelemetry.ActivitySource.StartActivity();
        activity?.SetTag("email.to", message.To.ToString());
        activity?.SetTag("email.template", message.Subject);

        try
        {
            await _smtpPipeline.ExecuteAsync(async ct =>
            {
                await connectionPool.SendAsync(message, ct);
            }, cancellationToken);

            string recipients = message.To.ToString();
            LogEmailSent(logger, recipients, message.Subject ?? string.Empty);
        }
        catch (Exception ex)
        {
            LogEmailFailed(logger, ex, message.To.ToString());

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new()
            {
                { "exception.type", ex.GetType().FullName },
                { "exception.message", ex.Message }
            }));

            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent successfully to {To} with subject '{Subject}'")]
    private static partial void LogEmailSent(ILogger logger, string to, string subject);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send email to {To} after all retry attempts")]
    private static partial void LogEmailFailed(ILogger logger, Exception ex, string to);
}
