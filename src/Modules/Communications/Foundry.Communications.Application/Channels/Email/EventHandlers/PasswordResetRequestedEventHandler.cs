using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Enums;
using Foundry.Shared.Contracts.Communications.Email;
using Foundry.Shared.Contracts.Identity.Events;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Application.Channels.Email.EventHandlers;

/// <summary>
/// Handles PasswordResetRequestedEvent from the Identity module.
/// Sends a password reset email to users who have requested a password reset if they have not disabled system notifications.
/// </summary>
public sealed partial class PasswordResetRequestedEventHandler
{
    public static async Task HandleAsync(
        PasswordResetRequestedEvent integrationEvent,
        IEmailPreferenceRepository preferenceRepository,
        IEmailTemplateService templateService,
        IEmailService emailService,
        ILogger<PasswordResetRequestedEventHandler> logger,
        CancellationToken cancellationToken)
    {
        LogHandlingPasswordReset(logger, integrationEvent.UserId, integrationEvent.Email);

        EmailPreference? preference = await preferenceRepository.GetByUserAndTypeAsync(
            integrationEvent.UserId,
            NotificationType.SystemNotification,
            cancellationToken);

        if (preference is not null && !preference.IsEnabled)
        {
            LogPasswordResetSkipped(logger, integrationEvent.UserId);
            return;
        }

        var model = new
        {
            Email = integrationEvent.Email,
            ResetToken = integrationEvent.ResetToken
        };

        string body = await templateService.RenderAsync("PasswordReset", model, cancellationToken);

        await emailService.SendAsync(
            to: integrationEvent.Email,
            from: null,
            subject: "Password Reset Request",
            body: body,
            cancellationToken: cancellationToken);

        LogPasswordResetSent(logger, integrationEvent.UserId, integrationEvent.Email);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling PasswordResetRequestedEvent for User {UserId} ({Email})")]
    private static partial void LogHandlingPasswordReset(ILogger logger, Guid userId, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} has disabled SystemNotification emails, skipping password reset email")]
    private static partial void LogPasswordResetSkipped(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset email sent to User {UserId} at {Email}")]
    private static partial void LogPasswordResetSent(ILogger logger, Guid userId, string email);
}
