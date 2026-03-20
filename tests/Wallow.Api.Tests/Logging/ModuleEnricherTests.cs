using Wallow.Api.Logging;
using Serilog.Core;
using Serilog.Events;

namespace Wallow.Api.Tests.Logging;

public class ModuleEnricherTests
{
    private readonly ModuleEnricher _sut = new();
    private readonly LogEventPropertyFactory _propertyFactory = new();

    [Theory]
    [InlineData("Wallow.Billing.Application.Handlers", "Billing")]
    [InlineData("Wallow.Identity.Infrastructure.Services", "Identity")]
    [InlineData("Wallow.Notifications.Domain.Events", "Notifications")]
    [InlineData("Wallow.Messaging.Application.Handlers", "Messaging")]
    [InlineData("Wallow.Announcements.Infrastructure.Services", "Announcements")]
    [InlineData("Wallow.Api", "Api")]
    public void Enrich_WithWallowSourceContext_ExtractsModuleName(string sourceContext, string expectedModule)
    {
        LogEvent logEvent = CreateLogEventWithSourceContext(sourceContext);

        _sut.Enrich(logEvent, _propertyFactory);

        logEvent.Properties["Module"].ToString().Should().Be($"\"{expectedModule}\"");
    }

    [Fact]
    public void Enrich_WithNoSourceContext_DefaultsToSystem()
    {
        LogEvent logEvent = CreateLogEvent();

        _sut.Enrich(logEvent, _propertyFactory);

        logEvent.Properties["Module"].ToString().Should().Be("\"System\"");
    }

    [Theory]
    [InlineData("Microsoft.AspNetCore.Hosting")]
    [InlineData("System.Net.Http")]
    [InlineData("Serilog.Core")]
    public void Enrich_WithNonWallowSourceContext_DefaultsToSystem(string sourceContext)
    {
        LogEvent logEvent = CreateLogEventWithSourceContext(sourceContext);

        _sut.Enrich(logEvent, _propertyFactory);

        logEvent.Properties["Module"].ToString().Should().Be("\"System\"");
    }

    [Fact]
    public void Enrich_WhenModulePropertyAlreadyExists_DoesNotOverwrite()
    {
        LogEvent logEvent = CreateLogEventWithSourceContext("Wallow.Billing.Application.Handlers");
        logEvent.AddPropertyIfAbsent(_propertyFactory.CreateProperty("Module", "Existing"));

        _sut.Enrich(logEvent, _propertyFactory);

        logEvent.Properties["Module"].ToString().Should().Be("\"Existing\"");
    }

    private static LogEvent CreateLogEvent()
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplate("Test", []),
            []);
    }

    private static LogEvent CreateLogEventWithSourceContext(string sourceContext)
    {
        LogEventProperty property = new("SourceContext", new ScalarValue(sourceContext));

        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplate("Test", []),
            [property]);
    }

    private sealed class LogEventPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }
}
