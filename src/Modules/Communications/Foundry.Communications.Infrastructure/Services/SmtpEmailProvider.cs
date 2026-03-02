using System.Diagnostics;
using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Application.Channels.Email.Telemetry;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Foundry.Communications.Infrastructure.Services;

public sealed partial class SmtpEmailProvider(
    IOptions<SmtpSettings> settings,
    ILogger<SmtpEmailProvider> logger) : IEmailProvider
{
    private readonly SmtpSettings _settings = settings.Value;

    public async Task<EmailDeliveryResult> SendAsync(EmailDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        using MimeMessage message = request.Attachment.HasValue
            ? BuildMessageWithAttachment(request)
            : BuildMessage(request);

        try
        {
            await SendWithRetryAsync(message, cancellationToken);
            return new EmailDeliveryResult(true, null);
        }
        catch (Exception ex)
        {
            return new EmailDeliveryResult(false, ex.Message);
        }
    }

    private MimeMessage BuildMessage(EmailDeliveryRequest request)
    {
        MimeMessage message = new MimeMessage();

        message.From.Add(string.IsNullOrWhiteSpace(request.From)
            ? new MailboxAddress(_settings.DefaultFromName, _settings.DefaultFromAddress)
            : MailboxAddress.Parse(request.From));

        message.To.Add(MailboxAddress.Parse(request.To));
        message.Subject = request.Subject;

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

        MimeMessage message = new MimeMessage();

        message.From.Add(string.IsNullOrWhiteSpace(request.From)
            ? new MailboxAddress(_settings.DefaultFromName, _settings.DefaultFromAddress)
            : MailboxAddress.Parse(request.From));

        message.To.Add(MailboxAddress.Parse(request.To));
        message.Subject = request.Subject;

        BodyBuilder builder = new BodyBuilder
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

    private async Task SendWithRetryAsync(MimeMessage message, CancellationToken cancellationToken)
    {
        using Activity? activity = EmailModuleTelemetry.ActivitySource.StartActivity("Email.Send");
        activity?.SetTag("email.to", message.To.ToString());
        activity?.SetTag("email.template", message.Subject);

        int attempt = 0;
        Exception? lastException = null;

        while (attempt < _settings.MaxRetries)
        {
            attempt++;

            try
            {
                using SmtpClient client = new SmtpClient();
                client.Timeout = _settings.TimeoutSeconds * 1000;

                SecureSocketOptions secureSocketOptions = _settings.UseSsl
                    ? SecureSocketOptions.StartTls
                    : SecureSocketOptions.None;

                await client.ConnectAsync(_settings.Host, _settings.Port, secureSocketOptions, cancellationToken);

                if (!string.IsNullOrWhiteSpace(_settings.Username) && !string.IsNullOrWhiteSpace(_settings.Password))
                {
                    await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
                }

                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                string recipients = message.To.ToString();
                LogEmailSent(logger, recipients, message.Subject, attempt);

                return;
            }
            catch (Exception ex)
            {
                lastException = ex;

                LogEmailAttemptFailed(logger, ex, message.To.ToString(), attempt, _settings.MaxRetries);

                if (attempt < _settings.MaxRetries)
                {
                    int delayMs = (int)Math.Pow(2, attempt) * 1000;
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
        }

        LogEmailAllAttemptsFailed(logger, lastException, message.To.ToString(), _settings.MaxRetries);

        activity?.SetStatus(ActivityStatusCode.Error, lastException?.Message ?? "All retry attempts failed");
        if (lastException is not null)
        {
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", lastException.GetType().FullName },
                { "exception.message", lastException.Message }
            }));
        }

        throw new InvalidOperationException(
            $"Failed to send email after {_settings.MaxRetries} attempts. See inner exception for details.",
            lastException);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sent successfully to {To} with subject '{Subject}' on attempt {Attempt}")]
    private static partial void LogEmailSent(ILogger logger, string to, string subject, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to send email to {To} on attempt {Attempt}/{MaxRetries}")]
    private static partial void LogEmailAttemptFailed(ILogger logger, Exception ex, string to, int attempt, int maxRetries);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send email to {To} after {MaxRetries} attempts")]
    private static partial void LogEmailAllAttemptsFailed(ILogger logger, Exception? ex, string to, int maxRetries);
}
