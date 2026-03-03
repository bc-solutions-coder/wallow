using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Domain.Enums;
using Foundry.Shared.Contracts.Communications.Announcements.Events;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Application.Channels.InApp.EventHandlers;

/// <summary>
/// Handles AnnouncementPublishedEvent to send push notifications for important announcements.
/// Only sends notifications for pinned announcements or Alert-type announcements.
/// </summary>
public sealed partial class AnnouncementPublishedEventHandler
{
    public static async Task HandleAsync(
        AnnouncementPublishedEvent integrationEvent,
        INotificationService notificationService,
        ITenantContext tenantContext,
        ILogger<AnnouncementPublishedEventHandler> logger,
        CancellationToken cancellationToken)
    {
        // Only send push notifications for important announcements (pinned or alerts)
        if (!integrationEvent.IsPinned && integrationEvent.Type != "Alert")
        {
            LogSkippingNonPriorityAnnouncement(logger, integrationEvent.AnnouncementId);
            return;
        }

        LogHandlingAnnouncementPublished(logger, integrationEvent.AnnouncementId, integrationEvent.Title);

        string title = GetNotificationTitle(integrationEvent);
        string message = TruncateContent(integrationEvent.Content, 200);

        // For targeted announcements, we need to send to specific users based on target
        // For now, we'll create a broadcast notification that the frontend can filter
        // In a full implementation, you'd query users matching the target criteria

        // If targeting all users or this specific tenant, broadcast to all
        if (integrationEvent.Target == "All" ||
            (integrationEvent.Target == "Tenant" &&
             integrationEvent.TargetValue == tenantContext.TenantId.Value.ToString()))
        {
            await notificationService.BroadcastToTenantAsync(
                tenantContext.TenantId,
                title,
                message,
                nameof(NotificationType.Announcement),
                cancellationToken);

            LogBroadcastAnnouncement(logger, integrationEvent.AnnouncementId, tenantContext.TenantId.Value);
        }
    }

    private static string GetNotificationTitle(AnnouncementPublishedEvent evt)
    {
        return evt.Type switch
        {
            "Alert" => $"Important: {evt.Title}",
            "Maintenance" => $"Maintenance Notice: {evt.Title}",
            "Feature" => $"New Feature: {evt.Title}",
            "Update" => $"Update: {evt.Title}",
            _ => evt.Title
        };
    }

    private static string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        // Strip markdown for notification preview
        string plainText = StripMarkdown(content);

        if (plainText.Length <= maxLength)
        {
            return plainText;
        }

        return plainText[..(maxLength - 3)] + "...";
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"```[\s\S]*?```", System.Text.RegularExpressions.RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial System.Text.RegularExpressions.Regex CodeBlockRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"`[^`]+`", System.Text.RegularExpressions.RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial System.Text.RegularExpressions.Regex InlineCodeRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"^#{1,6}\s*", System.Text.RegularExpressions.RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial System.Text.RegularExpressions.Regex HeaderRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)", System.Text.RegularExpressions.RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial System.Text.RegularExpressions.Regex LinkRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"\*{1,2}([^*]+)\*{1,2}", System.Text.RegularExpressions.RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial System.Text.RegularExpressions.Regex BoldItalicAsteriskRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"_{1,2}([^_]+)_{1,2}", System.Text.RegularExpressions.RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial System.Text.RegularExpressions.Regex BoldItalicUnderscoreRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"\s+", System.Text.RegularExpressions.RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial System.Text.RegularExpressions.Regex WhitespaceRegex();

    private static string StripMarkdown(string markdown)
    {
        // Simple markdown stripping for notification preview
        // Remove headers, links, bold, italic, code blocks
        string result = markdown;

        // Remove code blocks
        result = CodeBlockRegex().Replace(result, "");
        result = InlineCodeRegex().Replace(result, "");

        // Remove headers
        result = HeaderRegex().Replace(result, "");

        // Remove links but keep text
        result = LinkRegex().Replace(result, "$1");

        // Remove bold/italic
        result = BoldItalicAsteriskRegex().Replace(result, "$1");
        result = BoldItalicUnderscoreRegex().Replace(result, "$1");

        // Normalize whitespace
        result = WhitespaceRegex().Replace(result, " ");

        return result.Trim();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping notification for non-priority announcement {AnnouncementId}")]
    private static partial void LogSkippingNonPriorityAnnouncement(ILogger logger, Guid announcementId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling AnnouncementPublishedEvent: Announcement {AnnouncementId} - {Title}")]
    private static partial void LogHandlingAnnouncementPublished(ILogger logger, Guid announcementId, string title);

    [LoggerMessage(Level = LogLevel.Information, Message = "Broadcast announcement notification for {AnnouncementId} to tenant {TenantId}")]
    private static partial void LogBroadcastAnnouncement(ILogger logger, Guid announcementId, Guid tenantId);
}
