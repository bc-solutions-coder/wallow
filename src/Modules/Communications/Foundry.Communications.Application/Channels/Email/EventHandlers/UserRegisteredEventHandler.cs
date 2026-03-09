using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Enums;
using Foundry.Shared.Contracts.Communications.Email;
using Foundry.Shared.Contracts.Identity.Events;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Application.Channels.Email.EventHandlers;

/// <summary>
/// Handles UserRegisteredEvent from the Identity module.
/// Sends a welcome email to newly registered users if they have not disabled system notifications.
/// </summary>
public sealed partial class UserRegisteredEventHandler
{
    public static async Task HandleAsync(
        UserRegisteredEvent integrationEvent,
        IEmailPreferenceRepository preferenceRepository,
        IEmailTemplateService templateService,
        IEmailService emailService,
        ILogger<UserRegisteredEventHandler> logger,
        CancellationToken cancellationToken)
    {
        LogHandlingUserRegistered(logger, integrationEvent.UserId, integrationEvent.Email);

        EmailPreference? preference = await preferenceRepository.GetByUserAndTypeAsync(
            integrationEvent.UserId,
            NotificationType.SystemNotification,
            cancellationToken);

        if (preference is not null && !preference.IsEnabled)
        {
            LogWelcomeEmailSkipped(logger, integrationEvent.UserId);
            return;
        }

        var model = new
        {
            integrationEvent.FirstName,
            integrationEvent.LastName,
            integrationEvent.Email
        };

        string body = await templateService.RenderAsync("WelcomeEmail", model, cancellationToken);

        await emailService.SendAsync(
            to: integrationEvent.Email,
            from: null,
            subject: "Welcome to Foundry!",
            body: body,
            cancellationToken: cancellationToken);

        LogWelcomeEmailSent(logger, integrationEvent.UserId, integrationEvent.Email);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling UserRegisteredEvent for User {UserId} ({Email})")]
    private static partial void LogHandlingUserRegistered(ILogger logger, Guid userId, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} has disabled SystemNotification emails, skipping welcome email")]
    private static partial void LogWelcomeEmailSkipped(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Welcome email sent to User {UserId} at {Email}")]
    private static partial void LogWelcomeEmailSent(ILogger logger, Guid userId, string email);
}
