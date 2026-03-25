using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Wallow.Notifications.Infrastructure.Services;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public class SimpleEmailTemplateServiceTests
{
#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    private readonly SimpleEmailTemplateService _service = new(
        LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Trace))
            .CreateLogger<SimpleEmailTemplateService>(),
        new ConfigurationBuilder().Build());
#pragma warning restore CA2000

    [Fact]
    public async Task RenderAsync_WelcomeEmailTemplate_ContainsPlaceholderValues()
    {
        object model = new { FirstName = "John", LastName = "Doe", Email = "john@test.com" };

        string result = await _service.RenderAsync("welcomeemail", model);

        result.Should().Contain("John");
        result.Should().Contain("Doe");
        result.Should().Contain("john@test.com");
        result.Should().NotContain("{{FirstName}}");
    }

    [Fact]
    public async Task RenderAsync_PasswordResetTemplate_ContainsResetUrl()
    {
        object model = new { Email = "user@test.com", ResetUrl = "https://app.test/reset?token=abc-123" };

        string result = await _service.RenderAsync("passwordreset", model);

        result.Should().Contain("https://app.test/reset?token=abc-123");
        result.Should().Contain("user@test.com");
        result.Should().Contain("Password Reset Request");
        result.Should().Contain("Reset Password");
    }

    [Fact]
    public async Task RenderAsync_UnknownTemplate_ReturnsDefaultTemplate()
    {
        object model = new { Message = "Custom message" };

        string result = await _service.RenderAsync("unknowntemplate", model);

        result.Should().Contain("Custom message");
        result.Should().NotContain("{{Message}}");
    }

    [Fact]
    public async Task RenderAsync_BillingInvoiceTemplate_ReplacesInvoiceData()
    {
        object model = new { InvoiceNumber = "INV-001", Amount = "$100.00", DueDate = "2026-04-01" };

        string result = await _service.RenderAsync("billinginvoice", model);

        result.Should().Contain("INV-001");
        result.Should().Contain("$100.00");
        result.Should().Contain("2026-04-01");
    }

    [Fact]
    public async Task RenderAsync_SystemNotificationTemplate_ReplacesMessage()
    {
        object model = new { Message = "Server maintenance at 3am" };

        string result = await _service.RenderAsync("systemnotification", model);

        result.Should().Contain("Server maintenance at 3am");
    }

    [Theory]
    [InlineData("taskcreated")]
    [InlineData("taskassigned")]
    [InlineData("taskcompleted")]
    public async Task RenderAsync_TaskTemplates_ReturnHtml(string templateName)
    {
        object model = new { TaskTitle = "My Task", TaskDescription = "Desc", AssignedTo = "User", CompletedBy = "User", CompletedAt = "Now", DueDate = "Tomorrow" };

        string result = await _service.RenderAsync(templateName, model);

        result.Should().Contain("<!DOCTYPE html>");
        result.Should().Contain("My Task");
    }

    [Fact]
    public async Task RenderAsync_DataRequestReceivedTemplate_ReplacesAllPlaceholders()
    {
        object model = new { RequestType = "export", RequestId = "REQ-001", RequestedAt = "2026-03-13" };

        string result = await _service.RenderAsync("datarequestreceived", model);

        result.Should().Contain("export");
        result.Should().Contain("REQ-001");
        result.Should().Contain("2026-03-13");
        result.Should().Contain("Data Request Received");
        result.Should().NotContain("{{RequestType}}");
        result.Should().NotContain("{{RequestId}}");
    }

    [Fact]
    public async Task RenderAsync_DataExportReadyTemplate_ReplacesAllPlaceholders()
    {
        object model = new { RequestId = "REQ-002", FileSizeFormatted = "15.3 MB", DownloadUrl = "https://example.com/download", ExpiresAt = "2026-04-13" };

        string result = await _service.RenderAsync("dataexportready", model);

        result.Should().Contain("REQ-002");
        result.Should().Contain("15.3 MB");
        result.Should().Contain("https://example.com/download");
        result.Should().Contain("2026-04-13");
        result.Should().Contain("Your Data Export is Ready");
    }

    [Fact]
    public async Task RenderAsync_DataErasureCompleteTemplate_ReplacesAllPlaceholders()
    {
        object model = new { RequestId = "REQ-003", CompletedAt = "2026-03-12T14:00:00Z" };

        string result = await _service.RenderAsync("dataerasurecomplete", model);

        result.Should().Contain("REQ-003");
        result.Should().Contain("2026-03-12T14:00:00Z");
        result.Should().Contain("Data Erasure Completed");
    }

    [Fact]
    public async Task RenderAsync_DataRequestRejectedTemplate_ReplacesAllPlaceholders()
    {
        object model = new { RequestType = "erasure", RequestId = "REQ-004", RejectionReason = "Unable to verify identity" };

        string result = await _service.RenderAsync("datarequestrejected", model);

        result.Should().Contain("erasure");
        result.Should().Contain("REQ-004");
        result.Should().Contain("Unable to verify identity");
        result.Should().Contain("Data Request Update");
    }

    [Fact]
    public async Task RenderAsync_DataRequestVerificationRequiredTemplate_ReplacesAllPlaceholders()
    {
        object model = new { RequestType = "export", RequestId = "REQ-005", VerificationUrl = "https://app.test/verify?token=abc-123" };

        string result = await _service.RenderAsync("datarequestverificationrequired", model);

        result.Should().Contain("export");
        result.Should().Contain("REQ-005");
        result.Should().Contain("https://app.test/verify?token=abc-123");
        result.Should().Contain("Verify Identity");
        result.Should().Contain("Verification Required");
    }

    [Fact]
    public async Task RenderAsync_IsCaseInsensitive_ForTemplateName()
    {
        object model = new { FirstName = "Jane", LastName = "Smith", Email = "jane@test.com" };

        string result = await _service.RenderAsync("WelcomeEmail", model);

        result.Should().Contain("Jane");
        result.Should().Contain("Welcome to Wallow!");
    }

    [Fact]
    public async Task RenderAsync_WithNullPropertyValue_ReplacesWithEmptyString()
    {
        object model = new { Message = (string?)null };

        string result = await _service.RenderAsync("systemnotification", model);

        result.Should().NotContain("{{Message}}");
        result.Should().Contain("<!DOCTYPE html>");
    }

    [Fact]
    public async Task RenderAsync_WithNumericProperty_ConvertsToString()
    {
        object model = new { Message = 42 };

        string result = await _service.RenderAsync("systemnotification", model);

        result.Should().Contain("42");
    }

    [Fact]
    public async Task RenderAsync_TaskCreatedTemplate_ReplacesSpecificFields()
    {
        object model = new { TaskTitle = "Fix Bug #123", TaskDescription = "Null reference in handler", AssignedTo = "dev@team.com" };

        string result = await _service.RenderAsync("taskcreated", model);

        result.Should().Contain("Fix Bug #123");
        result.Should().Contain("Null reference in handler");
        result.Should().Contain("dev@team.com");
        result.Should().Contain("New Task Created");
    }

    [Fact]
    public async Task RenderAsync_TaskAssignedTemplate_ReplacesSpecificFields()
    {
        object model = new { TaskTitle = "Deploy v2", TaskDescription = "Deploy to staging", DueDate = "2026-03-20" };

        string result = await _service.RenderAsync("taskassigned", model);

        result.Should().Contain("Deploy v2");
        result.Should().Contain("Deploy to staging");
        result.Should().Contain("2026-03-20");
        result.Should().Contain("Task Assigned to You");
    }

    [Fact]
    public async Task RenderAsync_TaskCompletedTemplate_ReplacesSpecificFields()
    {
        object model = new { TaskTitle = "Review PR", CompletedBy = "alice@team.com", CompletedAt = "2026-03-13 10:00" };

        string result = await _service.RenderAsync("taskcompleted", model);

        result.Should().Contain("Review PR");
        result.Should().Contain("alice@team.com");
        result.Should().Contain("2026-03-13 10:00");
        result.Should().Contain("Task Completed");
    }

    [Fact]
    public async Task RenderAsync_PasswordResetTemplate_ContainsExpirationNotice()
    {
        object model = new { Email = "user@test.com", ResetUrl = "https://app.test/reset?token=abc" };

        string result = await _service.RenderAsync("passwordreset", model);

        result.Should().Contain("expire in 24 hours");
        result.Should().Contain("Password Reset Request");
    }

    [Fact]
    public async Task RenderAsync_WelcomeEmailTemplate_ContainsFullStructure()
    {
        object model = new { FirstName = "Test", LastName = "User", Email = "test@example.com", AppUrl = "https://app.test" };

        string result = await _service.RenderAsync("welcomeemail", model);

        result.Should().Contain("Welcome to Wallow!");
        result.Should().Contain("The Wallow Team");
        result.Should().Contain("<!DOCTYPE html>");
        result.Should().Contain("Go to Dashboard");
        result.Should().Contain("https://app.test");
        result.Should().Contain("table role=\"presentation\"");
    }

    [Fact]
    public async Task RenderAsync_EmailVerificationTemplate_ContainsVerifyUrl()
    {
        object model = new { FirstName = "Jane", LastName = "Doe", VerifyUrl = "https://app.test/verify?token=xyz-456" };

        string result = await _service.RenderAsync("emailverification", model);

        result.Should().Contain("Jane");
        result.Should().Contain("Doe");
        result.Should().Contain("https://app.test/verify?token=xyz-456");
        result.Should().Contain("Verify Your Email");
        result.Should().Contain("Verify Email");
        result.Should().Contain("<!DOCTYPE html>");
        result.Should().NotContain("{{VerifyUrl}}");
        result.Should().NotContain("{{FirstName}}");
    }

    [Fact]
    public async Task RenderAsync_PasswordChangedTemplate_ReturnsNonNull()
    {
        object model = new { Email = "user@test.com", AppUrl = "https://app.test" };

        string result = await _service.RenderAsync("passwordchanged", model);

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RenderAsync_PasswordChangedTemplate_ContainsEmailAndSignInCta()
    {
        object model = new { Email = "user@test.com", AppUrl = "https://app.test" };

        string result = await _service.RenderAsync("passwordchanged", model);

        result.Should().Contain("user@test.com");
        result.Should().Contain("Sign In");
        result.Should().Contain("https://app.test");
        result.Should().Contain("Your Password Has Been Changed");
        result.Should().Contain("<!DOCTYPE html>");
        result.Should().NotContain("{{Email}}");
        result.Should().NotContain("{{AppUrl}}");
    }

    [Fact]
    public async Task RenderAsync_PasswordChangedTemplate_ContainsAppName()
    {
        object model = new { Email = "user@test.com", AppUrl = "https://app.test" };

        string result = await _service.RenderAsync("passwordchanged", model);

        result.Should().Contain("Wallow");
        result.Should().NotContain("{{AppName}}");
    }

#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    [Fact]
    public async Task RenderAsync_WithLoggingDisabled_StillRendersTemplate()
    {
        SimpleEmailTemplateService service = new(
            LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.None))
                .CreateLogger<SimpleEmailTemplateService>(),
            new ConfigurationBuilder().Build());
        object model = new { Message = "Test notification" };

        string result = await service.RenderAsync("systemnotification", model);

        result.Should().Contain("Test notification");
        result.Should().Contain("System Notification");
    }
#pragma warning restore CA2000

    [Fact]
    public async Task RenderAsync_InquiryCommentTemplate_ContainsAllPlaceholders()
    {
        object model = new
        {
            SubmitterName = "Alice",
            AuthorName = "Bob",
            InquirySubject = "Project Proposal",
            CommentContent = "Looks great, let's proceed.",
            InquiryUrl = "https://app.test/inquiries/123"
        };

        string result = await _service.RenderAsync("inquirycomment", model);

        result.Should().Contain("Alice");
        result.Should().Contain("Bob");
        result.Should().Contain("Project Proposal");
        result.Should().Contain("Looks great, let&#39;s proceed.");
        result.Should().Contain("https://app.test/inquiries/123");
        result.Should().Contain("New Comment on Your Inquiry");
        result.Should().Contain("View Inquiry");
        result.Should().Contain("<!DOCTYPE html>");
        result.Should().NotContain("{{SubmitterName}}");
        result.Should().NotContain("{{AuthorName}}");
    }

    [Fact]
    public async Task RenderAsync_EmailVerificationTemplate_ContainsAppName()
    {
        object model = new { FirstName = "Test", LastName = "User", VerifyUrl = "https://app.test/verify" };

        string result = await _service.RenderAsync("emailverification", model);

        result.Should().Contain("Wallow");
        result.Should().NotContain("{{AppName}}");
    }

#pragma warning disable CA2000 // LoggerFactory disposal not needed in tests
    [Fact]
    public async Task RenderAsync_WithCustomAppName_UsesConfiguredName()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Branding:AppName"] = "MyApp" })
            .Build();
        SimpleEmailTemplateService service = new(
            LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Trace))
                .CreateLogger<SimpleEmailTemplateService>(),
            config);
        object model = new { FirstName = "Test", LastName = "User", Email = "test@example.com" };

        string result = await service.RenderAsync("welcomeemail", model);

        result.Should().Contain("MyApp");
        result.Should().Contain("Welcome to MyApp!");
        result.Should().NotContain("{{AppName}}");
    }
#pragma warning restore CA2000
}
