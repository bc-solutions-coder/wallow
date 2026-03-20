using System.Globalization;
using System.Reflection;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Microsoft.Extensions.Logging;

namespace Wallow.Notifications.Infrastructure.Services;

public sealed partial class SimpleEmailTemplateService(ILogger<SimpleEmailTemplateService> logger) : IEmailTemplateService
{

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
            "welcomeemail" => """
                <html>
                <body>
                    <h2>Welcome to Wallow!</h2>
                    <p>Hi {{FirstName}} {{LastName}},</p>
                    <p>Thank you for registering with Wallow. We're excited to have you on board!</p>
                    <p>Your account has been successfully created with the email: <strong>{{Email}}</strong></p>
                    <p>You can now start exploring all the features we have to offer.</p>
                    <p>If you have any questions, feel free to reach out to our support team.</p>
                    <p>Best regards,<br/>The Wallow Team</p>
                </body>
                </html>
                """,

            "taskcreated" => """
                <html>
                <body>
                    <h2>New Task Created</h2>
                    <p>A new task has been created: <strong>{{TaskTitle}}</strong></p>
                    <p>Description: {{TaskDescription}}</p>
                    <p>Assigned to: {{AssignedTo}}</p>
                </body>
                </html>
                """,

            "taskassigned" => """
                <html>
                <body>
                    <h2>Task Assigned to You</h2>
                    <p>You have been assigned a new task: <strong>{{TaskTitle}}</strong></p>
                    <p>Description: {{TaskDescription}}</p>
                    <p>Due Date: {{DueDate}}</p>
                </body>
                </html>
                """,

            "taskcompleted" => """
                <html>
                <body>
                    <h2>Task Completed</h2>
                    <p>The task <strong>{{TaskTitle}}</strong> has been completed.</p>
                    <p>Completed by: {{CompletedBy}}</p>
                    <p>Completed at: {{CompletedAt}}</p>
                </body>
                </html>
                """,

            "billinginvoice" => """
                <html>
                <body>
                    <h2>New Invoice</h2>
                    <p>Invoice #{{InvoiceNumber}} is ready for review.</p>
                    <p>Amount: {{Amount}}</p>
                    <p>Due Date: {{DueDate}}</p>
                </body>
                </html>
                """,

            "systemnotification" => """
                <html>
                <body>
                    <h2>System Notification</h2>
                    <p>{{Message}}</p>
                </body>
                </html>
                """,

            "passwordreset" => """
                <html>
                <body>
                    <h2>Password Reset Request</h2>
                    <p>We received a request to reset your password for the account associated with {{Email}}.</p>
                    <p>Your password reset token is: <strong>{{ResetToken}}</strong></p>
                    <p>If you did not request this password reset, please ignore this email or contact support if you have concerns.</p>
                    <p>This token will expire in 24 hours.</p>
                    <p>Best regards,<br/>The Wallow Team</p>
                </body>
                </html>
                """,

            "datarequestreceived" => """
                <html>
                <body>
                    <h2>Data Request Received</h2>
                    <p>We have received your {{RequestType}} request (ID: <strong>{{RequestId}}</strong>).</p>
                    <p>Request submitted on: {{RequestedAt}}</p>
                    <p>We will process your request and notify you once it's complete. This typically takes up to 30 days as required by data protection regulations.</p>
                    <p>If you have any questions about your request, please contact our privacy team and reference the request ID above.</p>
                    <p>Best regards,<br/>The Wallow Team</p>
                </body>
                </html>
                """,

            "dataexportready" => """
                <html>
                <body>
                    <h2>Your Data Export is Ready</h2>
                    <p>Your data export request (ID: <strong>{{RequestId}}</strong>) has been completed and is ready for download.</p>
                    <p>File size: {{FileSizeFormatted}}</p>
                    <p>Download link: <a href="{{DownloadUrl}}">{{DownloadUrl}}</a></p>
                    <p><strong>Important:</strong> This download link will expire on {{ExpiresAt}}. Please download your data before this date.</p>
                    <p>The export contains all personal data we hold about you in accordance with data protection regulations.</p>
                    <p>Best regards,<br/>The Wallow Team</p>
                </body>
                </html>
                """,

            "dataerasurecomplete" => """
                <html>
                <body>
                    <h2>Data Erasure Completed</h2>
                    <p>Your data erasure request (ID: <strong>{{RequestId}}</strong>) has been completed.</p>
                    <p>Completed on: {{CompletedAt}}</p>
                    <p>Your personal data has been permanently deleted from our systems in accordance with data protection regulations.</p>
                    <p>Please note that some data may be retained for legal compliance purposes as required by law.</p>
                    <p>If you have any questions, please contact our privacy team.</p>
                    <p>Best regards,<br/>The Wallow Team</p>
                </body>
                </html>
                """,

            "datarequestrejected" => """
                <html>
                <body>
                    <h2>Data Request Update</h2>
                    <p>Your {{RequestType}} request (ID: <strong>{{RequestId}}</strong>) could not be processed.</p>
                    <p>Reason: {{RejectionReason}}</p>
                    <p>If you believe this decision is incorrect or would like further clarification, please contact our privacy team and reference the request ID above.</p>
                    <p>Best regards,<br/>The Wallow Team</p>
                </body>
                </html>
                """,

            "datarequestverificationrequired" => """
                <html>
                <body>
                    <h2>Verification Required for Your Data Request</h2>
                    <p>We have received your {{RequestType}} request (ID: <strong>{{RequestId}}</strong>).</p>
                    <p>To protect your privacy, we need to verify your identity before processing this request.</p>
                    <p>Verification token: <strong>{{VerificationToken}}</strong></p>
                    <p>Please provide this token through your account settings or contact our privacy team to complete the verification process.</p>
                    <p>This verification link will expire in 48 hours.</p>
                    <p>Best regards,<br/>The Wallow Team</p>
                </body>
                </html>
                """,

            _ => """
                <html>
                <body>
                    <p>{{Message}}</p>
                </body>
                </html>
                """
        };
    }

    private static string RenderTemplate(string template, object model)
    {
        string result = template;
        PropertyInfo[] properties = model.GetType().GetProperties();

        foreach (PropertyInfo property in properties)
        {
            string placeholder = $"{{{{{property.Name}}}}}";
            string value = Convert.ToString(property.GetValue(model), CultureInfo.InvariantCulture) ?? string.Empty;
            result = result.Replace(placeholder, value, StringComparison.Ordinal);
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rendered template '{TemplateName}'")]
    private static partial void LogRenderedTemplate(ILogger logger, string templateName);
}
