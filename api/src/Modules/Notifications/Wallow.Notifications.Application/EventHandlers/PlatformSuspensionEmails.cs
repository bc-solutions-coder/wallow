using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Shared.Kernel.Configuration;
using Wolverine;

namespace Wallow.Notifications.Application.EventHandlers;

/// <summary>
/// The shape the organization-lifecycle notices share: the address of the relevant page in the
/// web app, and one email per recipient. Kept in one place so the organization and client
/// handlers cannot drift apart in how they resolve the web URL or fan out.
/// </summary>
internal static class PlatformSuspensionEmails
{
    /// <summary>The organization's page in the web app — the link a suspension notice points at.</summary>
    public static string OrganizationUrl(IConfiguration configuration, Guid organizationId)
    {
        return $"{WebUrl(configuration)}/dashboard/organizations/{organizationId}";
    }

    /// <summary>
    /// The organizations list — where a deletion notice points, since the deleted
    /// organization's own page no longer exists.
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

    /// <summary>One email per recipient, sent inline so a failure surfaces to the handler.</summary>
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
