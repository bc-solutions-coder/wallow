using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Shared.Kernel.Configuration;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

/// <summary>
/// Shared URL resolution and email dispatch for organization lifecycle notices.
/// </summary>
internal static class PlatformSuspensionEmails
{
    public static string OrganizationUrl(IConfiguration configuration, Guid organizationId)
    {
        return $"{WebUrl(configuration)}/dashboard/organizations/{organizationId}";
    }

    /// <summary>
    /// Links deletion notices to the organizations list because the organization page is gone.
    /// </summary>
    public static string DashboardUrl(IConfiguration configuration)
    {
        return $"{WebUrl(configuration)}/dashboard/organizations";
    }

    private static string WebUrl(IConfiguration configuration)
    {
        string webUrl = configuration["ServiceUrls:WebUrl"]
                        ?? configuration["WebUrl"]
                        ?? new ServiceUrlsOptions().WebUrl;

        return webUrl.TrimEnd('/');
    }

    /// <summary>
    /// Invokes email commands sequentially. Exceptions stop dispatch to remaining recipients;
    /// SendEmailHandler records delivery failures without throwing them.
    /// </summary>
    public static async Task SendAsync(
        IMessageBus bus,
        IReadOnlyList<string> recipients,
        string subject,
        string body)
    {
        foreach (string recipient in recipients)
        {
            SendEmailCommand emailCommand = new(
                To: recipient,
                From: null,
                Subject: subject,
                Body: body);

            await bus.InvokeAsync(emailCommand);
        }
    }
}
