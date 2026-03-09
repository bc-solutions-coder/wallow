using Foundry.Communications.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Tests.Infrastructure.Services;

public class SimpleEmailTemplateServiceEdgeCaseTests
{
    private readonly SimpleEmailTemplateService _service;

    public SimpleEmailTemplateServiceEdgeCaseTests()
    {
        ILogger<SimpleEmailTemplateService> logger = Substitute.For<ILogger<SimpleEmailTemplateService>>();
        _service = new SimpleEmailTemplateService(logger);
    }

    [Fact]
    public async Task RenderAsync_WithNullPropertyValue_ReplacesWithEmptyString()
    {
        object model = new { Message = (string?)null };

        string result = await _service.RenderAsync("systemnotification", model);

        result.Should().Contain("System Notification");
        result.Should().NotContain("{{Message}}");
    }

    [Fact]
    public async Task RenderAsync_WithIntegerProperty_ConvertsToString()
    {
        object model = new { Message = 42 };

        string result = await _service.RenderAsync("systemnotification", model);

        result.Should().Contain("42");
    }

    [Fact]
    public async Task RenderAsync_WithDecimalProperty_ConvertsToString()
    {
        object model = new { Amount = 100.50m, InvoiceNumber = "INV-100", DueDate = "2026-01-01" };

        string result = await _service.RenderAsync("billinginvoice", model);

        result.Should().Contain("100.5");
    }

    [Fact]
    public async Task RenderAsync_WithMixedCaseTemplateName_MatchesCaseInsensitively()
    {
        object model = new { Message = "Test" };

        string result = await _service.RenderAsync("SYSTEMNOTIFICATION", model);

        result.Should().Contain("System Notification");
    }

    [Fact]
    public async Task RenderAsync_WithEmptyModel_LeavesPlaceholders()
    {
        object model = new { };

        string result = await _service.RenderAsync("systemnotification", model);

        result.Should().Contain("{{Message}}");
    }

    [Fact]
    public async Task RenderAsync_WithExtraModelProperties_IgnoresExtras()
    {
        object model = new { Message = "Hello", ExtraField = "Ignored" };

        string result = await _service.RenderAsync("systemnotification", model);

        result.Should().Contain("Hello");
        result.Should().NotContain("ExtraField");
        result.Should().NotContain("Ignored");
    }

    [Fact]
    public async Task RenderAsync_WithBooleanProperty_ConvertsToString()
    {
        object model = new { Message = true };

        string result = await _service.RenderAsync("systemnotification", model);

        result.Should().Contain("True");
    }

    [Fact]
    public async Task RenderAsync_WithDateTimeProperty_ConvertsToString()
    {
        DateTime date = new(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        object model = new { RequestType = "export", RequestId = "REQ-100", RequestedAt = date.ToString("o") };

        string result = await _service.RenderAsync("datarequestreceived", model);

        result.Should().Contain("REQ-100");
        result.Should().Contain("export");
    }
}
