using Wallow.Notifications.Application.Channels.Email.DTOs;
using Wallow.Notifications.Domain.Channels.Email.Entities;

namespace Wallow.Notifications.Application.Channels.Email.Mappings;

public static class EmailMappings
{
    public static EmailDto ToDto(this EmailMessage emailMessage)
    {
        return new EmailDto(
            emailMessage.Id.Value,
            emailMessage.To.Value,
            emailMessage.From?.Value,
            emailMessage.Content.Subject,
            emailMessage.Content.Body,
            emailMessage.Status,
            emailMessage.SentAt,
            emailMessage.FailureReason,
            emailMessage.RetryCount,
            emailMessage.CreatedAt,
            emailMessage.UpdatedAt);
    }

    public static EmailPreferenceDto ToDto(this EmailPreference preference)
    {
        return new EmailPreferenceDto(
            preference.Id.Value,
            preference.UserId,
            preference.NotificationType,
            preference.IsEnabled,
            preference.CreatedAt,
            preference.UpdatedAt);
    }
}
