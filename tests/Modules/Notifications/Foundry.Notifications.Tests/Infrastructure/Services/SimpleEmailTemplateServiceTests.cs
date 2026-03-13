using Foundry.Notifications.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foundry.Notifications.Tests.Infrastructure.Services;

public class SimpleEmailTemplateServiceTests
{
    private readonly SimpleEmailTemplateService _service = new(NullLogger<SimpleEmailTemplateService>.Instance);

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
    public async Task RenderAsync_PasswordResetTemplate_ContainsToken()
    {
        object model = new { Email = "user@test.com", ResetToken = "token-abc-123" };

        string result = await _service.RenderAsync("passwordreset", model);

        result.Should().Contain("token-abc-123");
        result.Should().Contain("user@test.com");
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

        result.Should().Contain("<html>");
        result.Should().Contain("My Task");
    }
}
