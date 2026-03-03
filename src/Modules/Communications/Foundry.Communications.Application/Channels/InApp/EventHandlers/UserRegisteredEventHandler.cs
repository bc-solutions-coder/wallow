using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Enums;
using Foundry.Shared.Contracts.Identity.Events;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Application.Channels.InApp.EventHandlers;

public sealed partial class UserRegisteredEventHandler
{
    public static async Task HandleAsync(
        UserRegisteredEvent integrationEvent,
        INotificationRepository notificationRepository,
        INotificationService notificationService,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        ILogger<UserRegisteredEventHandler> logger,
        CancellationToken cancellationToken)
    {
        LogHandlingUserRegistered(logger, integrationEvent.UserId);

        string title = "Welcome to Foundry!";
        string message = $"Hi {integrationEvent.FirstName}, welcome to Foundry! Explore tasks, projects, and collaborate with your team.";

        Notification notification = Notification.Create(
            tenantContext.TenantId,
            integrationEvent.UserId,
            NotificationType.SystemAlert,
            title,
            message,
            timeProvider);

        notificationRepository.Add(notification);
        await notificationRepository.SaveChangesAsync(cancellationToken);

        await notificationService.SendToUserAsync(
            integrationEvent.UserId,
            title,
            message,
            nameof(NotificationType.SystemAlert),
            cancellationToken);

        LogWelcomeNotificationCreated(logger, integrationEvent.UserId);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling UserRegisteredEvent for User {UserId}")]
    private static partial void LogHandlingUserRegistered(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Welcome notification created for User {UserId}")]
    private static partial void LogWelcomeNotificationCreated(ILogger logger, Guid userId);
}
