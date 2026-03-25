using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed partial class SimpleEmailTemplateService(
    ILogger<SimpleEmailTemplateService> logger,
    IConfiguration configuration) : IEmailTemplateService
{
    private readonly string _appName = configuration["Branding:AppName"] ?? "Wallow";

    public Task<string> RenderAsync(string templateName, object model, CancellationToken cancellationToken = default)
    {
        string template = GetTemplate(templateName);
        string rendered = RenderTemplate(template, model);

        LogRenderedTemplate(logger, templateName);

        return Task.FromResult(rendered);
    }

    private static string GetTemplate(string templateName)
    {
        return templateName.ToLowerInvariant() switch
        {
            "welcomeemail" => WrapInLayout(
                "Welcome to {{AppName}}!",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">Hi {{FirstName}} {{LastName}},</p>
                        <p style="margin: 0 0 16px;">Thank you for registering with {{AppName}}. We're excited to have you on board!</p>
                        <p style="margin: 0 0 16px;">Your account has been successfully created with the email: <strong>{{Email}}</strong></p>
                        <p style="margin: 0 0 24px;">You can now start exploring all the features we have to offer.</p>
                    </td>
                </tr>
                """,
                "Go to Dashboard",
                "{{AppUrl}}"),

            "emailverification" => WrapInLayout(
                "Verify Your Email",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">Hi {{FirstName}} {{LastName}},</p>
                        <p style="margin: 0 0 16px;">Thank you for signing up for {{AppName}}. Please verify your email address to activate your account.</p>
                        <p style="margin: 0 0 24px;">If you did not create an account, you can safely ignore this email.</p>
                    </td>
                </tr>
                """,
                "Verify Email",
                "{{VerifyUrl}}"),

            "taskcreated" => WrapInLayout(
                "New Task Created",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">A new task has been created: <strong>{{TaskTitle}}</strong></p>
                        <p style="margin: 0 0 16px;">Description: {{TaskDescription}}</p>
                        <p style="margin: 0 0 16px;">Assigned to: {{AssignedTo}}</p>
                    </td>
                </tr>
                """),

            "taskassigned" => WrapInLayout(
                "Task Assigned to You",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">You have been assigned a new task: <strong>{{TaskTitle}}</strong></p>
                        <p style="margin: 0 0 16px;">Description: {{TaskDescription}}</p>
                        <p style="margin: 0 0 16px;">Due Date: {{DueDate}}</p>
                    </td>
                </tr>
                """),

            "taskcompleted" => WrapInLayout(
                "Task Completed",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">The task <strong>{{TaskTitle}}</strong> has been completed.</p>
                        <p style="margin: 0 0 16px;">Completed by: {{CompletedBy}}</p>
                        <p style="margin: 0 0 16px;">Completed at: {{CompletedAt}}</p>
                    </td>
                </tr>
                """),

            "billinginvoice" => WrapInLayout(
                "New Invoice",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">Invoice #{{InvoiceNumber}} is ready for review.</p>
                        <p style="margin: 0 0 16px;">Amount: {{Amount}}</p>
                        <p style="margin: 0 0 16px;">Due Date: {{DueDate}}</p>
                    </td>
                </tr>
                """),

            "systemnotification" => WrapInLayout(
                "System Notification",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">{{Message}}</p>
                    </td>
                </tr>
                """),

            "organizationmemberadded" => WrapInLayout(
                "You've Been Added to an Organization",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">You have been added as a member of an organization on {{AppName}}.</p>
                        <p style="margin: 0 0 24px;">Log in to view your new organization and get started.</p>
                    </td>
                </tr>
                """,
                "Log In",
                "{{AppUrl}}"),

            "inquirycomment" => WrapInLayout(
                "New Comment on Your Inquiry",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">Hi {{SubmitterName}},</p>
                        <p style="margin: 0 0 16px;"><strong>{{AuthorName}}</strong> has added a comment on your inquiry: <strong>{{InquirySubject}}</strong></p>
                        <p style="margin: 0 0 16px; padding: 12px 16px; background-color: #f9f9f9; border-left: 4px solid #4CAF50; font-style: italic;">{{CommentContent}}</p>
                        <p style="margin: 0 0 24px;">Click the button below to view the full conversation and reply.</p>
                    </td>
                </tr>
                """,
                "View Inquiry",
                "{{InquiryUrl}}"),

            "passwordchanged" => WrapInLayout(
                "Your Password Has Been Changed",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">The password for the account associated with <strong>{{Email}}</strong> has just been changed.</p>
                        <p style="margin: 0 0 16px;">If you made this change, no further action is needed.</p>
                        <p style="margin: 0 0 24px;">If you did not change your password, please contact support immediately to secure your account.</p>
                    </td>
                </tr>
                """,
                "Sign In",
                "{{AppUrl}}"),

            "passwordreset" => WrapInLayout(
                "Password Reset Request",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">We received a request to reset your password for the account associated with <strong>{{Email}}</strong>.</p>
                        <p style="margin: 0 0 16px;">Click the button below to set a new password. This link will expire in 24 hours.</p>
                        <p style="margin: 0 0 24px;">If you did not request this password reset, please ignore this email or contact support if you have concerns.</p>
                    </td>
                </tr>
                """,
                "Reset Password",
                "{{ResetUrl}}"),

            "datarequestreceived" => WrapInLayout(
                "Data Request Received",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">We have received your {{RequestType}} request (ID: <strong>{{RequestId}}</strong>).</p>
                        <p style="margin: 0 0 16px;">Request submitted on: {{RequestedAt}}</p>
                        <p style="margin: 0 0 16px;">We will process your request and notify you once it's complete. This typically takes up to 30 days as required by data protection regulations.</p>
                        <p style="margin: 0 0 16px;">If you have any questions about your request, please contact our privacy team and reference the request ID above.</p>
                    </td>
                </tr>
                """),

            "dataexportready" => WrapInLayout(
                "Your Data Export is Ready",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">Your data export request (ID: <strong>{{RequestId}}</strong>) has been completed and is ready for download.</p>
                        <p style="margin: 0 0 16px;">File size: {{FileSizeFormatted}}</p>
                        <p style="margin: 0 0 24px;"><strong>Important:</strong> This download link will expire on {{ExpiresAt}}. Please download your data before this date.</p>
                    </td>
                </tr>
                """,
                "Download Export",
                "{{DownloadUrl}}"),

            "dataerasurecomplete" => WrapInLayout(
                "Data Erasure Completed",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">Your data erasure request (ID: <strong>{{RequestId}}</strong>) has been completed.</p>
                        <p style="margin: 0 0 16px;">Completed on: {{CompletedAt}}</p>
                        <p style="margin: 0 0 16px;">Your personal data has been permanently deleted from our systems in accordance with data protection regulations.</p>
                        <p style="margin: 0 0 16px;">Please note that some data may be retained for legal compliance purposes as required by law.</p>
                        <p style="margin: 0 0 16px;">If you have any questions, please contact our privacy team.</p>
                    </td>
                </tr>
                """),

            "datarequestrejected" => WrapInLayout(
                "Data Request Update",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">Your {{RequestType}} request (ID: <strong>{{RequestId}}</strong>) could not be processed.</p>
                        <p style="margin: 0 0 16px;">Reason: {{RejectionReason}}</p>
                        <p style="margin: 0 0 16px;">If you believe this decision is incorrect or would like further clarification, please contact our privacy team and reference the request ID above.</p>
                    </td>
                </tr>
                """),

            "datarequestverificationrequired" => WrapInLayout(
                "Verification Required for Your Data Request",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">We have received your {{RequestType}} request (ID: <strong>{{RequestId}}</strong>).</p>
                        <p style="margin: 0 0 16px;">To protect your privacy, we need to verify your identity before processing this request.</p>
                        <p style="margin: 0 0 24px;">Click the button below to verify your identity. This link will expire in 48 hours.</p>
                    </td>
                </tr>
                """,
                "Verify Identity",
                "{{VerificationUrl}}"),

            _ => WrapInLayout(
                "Notification",
                """
                <tr>
                    <td style="padding: 30px 40px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; line-height: 1.6; color: #333333;">
                        <p style="margin: 0 0 16px;">{{Message}}</p>
                    </td>
                </tr>
                """)
        };
    }

    private static string WrapInLayout(string heading, string bodyRows)
    {
        return WrapInLayout(heading, bodyRows, ctaText: null, ctaUrl: null);
    }

    private static string WrapInLayout(string heading, string bodyRows, string? ctaText, string? ctaUrl)
    {
        string ctaSection = ctaText is not null && ctaUrl is not null
            ? """
                <tr>
                    <td align="center" style="padding: 0 40px 30px;">
                        <table role="presentation" cellpadding="0" cellspacing="0">
                            <tr>
                                <td style="border-radius: 4px; background-color: #4CAF50;">
                                    <a href="%%CTA_URL%%" target="_blank" style="display: inline-block; padding: 14px 32px; font-family: Arial, Helvetica, sans-serif; font-size: 16px; font-weight: bold; color: #ffffff; text-decoration: none;">%%CTA_TEXT%%</a>
                                </td>
                            </tr>
                        </table>
                    </td>
                </tr>
                """
                .Replace("%%CTA_TEXT%%", ctaText, StringComparison.Ordinal)
                .Replace("%%CTA_URL%%", ctaUrl, StringComparison.Ordinal)
            : string.Empty;

        return """
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8" /><meta name="viewport" content="width=device-width, initial-scale=1.0" /></head>
            <body style="margin: 0; padding: 0; background-color: #f4f4f4;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color: #f4f4f4;">
            <tr><td align="center" style="padding: 40px 0;">
            <table role="presentation" width="600" cellpadding="0" cellspacing="0" style="background-color: #ffffff; border-radius: 4px; overflow: hidden;">
                <tr>
                    <td style="background-color: #333333; padding: 24px 40px; text-align: center;">
                        <h1 style="margin: 0; font-family: Arial, Helvetica, sans-serif; font-size: 22px; color: #ffffff;">{{AppName}}</h1>
                    </td>
                </tr>
                <tr>
                    <td style="padding: 8px 40px 0; font-family: Arial, Helvetica, sans-serif;">
                        <h2 style="margin: 20px 0 0; font-size: 20px; color: #333333;">%%HEADING%%</h2>
                    </td>
                </tr>
                %%BODY%%
                %%CTA%%
                <tr>
                    <td style="background-color: #f9f9f9; padding: 20px 40px; text-align: center; font-family: Arial, Helvetica, sans-serif; font-size: 13px; color: #999999; line-height: 1.5;">
                        <p style="margin: 0;">Best regards, The {{AppName}} Team</p>
                        <p style="margin: 8px 0 0;">If you have any questions, feel free to reach out to our support team.</p>
                    </td>
                </tr>
            </table>
            </td></tr>
            </table>
            </body>
            </html>
            """
            .Replace("%%HEADING%%", heading, StringComparison.Ordinal)
            .Replace("%%BODY%%", bodyRows, StringComparison.Ordinal)
            .Replace("%%CTA%%", ctaSection, StringComparison.Ordinal);
    }

    private string RenderTemplate(string template, object model)
    {
        string result = template;

        // Inject branding before model properties so AppName is always available
        result = result.Replace("{{AppName}}", _appName, StringComparison.Ordinal);

        PropertyInfo[] properties = model.GetType().GetProperties();

        foreach (PropertyInfo property in properties)
        {
            string placeholder = $"{{{{{property.Name}}}}}";
            string value = System.Net.WebUtility.HtmlEncode(
                Convert.ToString(property.GetValue(model), CultureInfo.InvariantCulture) ?? string.Empty);
            result = result.Replace(placeholder, value, StringComparison.Ordinal);
        }

        // Strip any unreplaced placeholders so users never see raw {{...}} text
        return UnreplacedPlaceholderRegex().Replace(result, string.Empty);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rendered template '{TemplateName}'")]
    private static partial void LogRenderedTemplate(ILogger logger, string templateName);

    [GeneratedRegex(@"\{\{[A-Za-z0-9_]+\}\}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UnreplacedPlaceholderRegex();
}
