using System.Reflection;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Infrastructure.Handlers;
using Wallow.Shared.Contracts;
using Wallow.Shared.Contracts.Identity.Events;

namespace Wallow.Identity.Tests.Infrastructure;

public class SessionEvictedHandlerTests
{
    [Fact]
    public void Event_can_be_constructed_with_all_required_fields()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Guid sessionId = Guid.NewGuid();
        string reason = "password_changed";

        UserSessionEvictedEvent sut = new()
        {
            UserId = userId,
            TenantId = tenantId,
            SessionId = sessionId,
            Reason = reason
        };

        sut.UserId.Should().Be(userId);
        sut.TenantId.Should().Be(tenantId);
        sut.SessionId.Should().Be(sessionId);
        sut.Reason.Should().Be(reason);
        sut.EventId.Should().NotBeEmpty();
        sut.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Event_inherits_from_IntegrationEvent()
    {
        UserSessionEvictedEvent sut = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            Reason = "mfa_reset"
        };

        sut.Should().BeAssignableTo<IntegrationEvent>();
        sut.Should().BeAssignableTo<IIntegrationEvent>();
    }

    [Fact]
    public void Handler_is_a_static_class()
    {
        Type handlerType = typeof(SessionEvictedHandler);

        handlerType.IsAbstract.Should().BeTrue();
        handlerType.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Handler_has_static_Handle_method_accepting_event_and_logger()
    {
        MethodInfo? handleMethod = typeof(SessionEvictedHandler)
            .GetMethod("Handle", BindingFlags.Public | BindingFlags.Static);

        handleMethod.Should().NotBeNull();
        handleMethod!.IsStatic.Should().BeTrue();

        ParameterInfo[] parameters = handleMethod.GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.Should().Be<UserSessionEvictedEvent>();
        parameters[1].ParameterType.Should().Be<ILogger>();
    }

    [Fact]
    public void Handler_logs_eviction_with_structured_arguments()
    {
        Guid sessionId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();

        UserSessionEvictedEvent evictedEvent = new()
        {
            UserId = userId,
            TenantId = tenantId,
            SessionId = sessionId,
            Reason = "admin_revoke"
        };

        TestLogger logger = new();

        SessionEvictedHandler.Handle(evictedEvent, logger);

        logger.Entries.Should().HaveCount(1);
        logger.Entries[0].LogLevel.Should().Be(LogLevel.Information);
        logger.Entries[0].Message.Should().Contain(sessionId.ToString());
        logger.Entries[0].Message.Should().Contain(userId.ToString());
        logger.Entries[0].Message.Should().Contain(tenantId.ToString());
        logger.Entries[0].Message.Should().Contain("admin_revoke");
    }

    private sealed class TestLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public bool IsEnabled(LogLevel logLevel) => true;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry
            {
                LogLevel = logLevel,
                Message = formatter(state, exception)
            });
        }
    }

    private sealed class LogEntry
    {
        public required LogLevel LogLevel { get; init; }
        public required string Message { get; init; }
    }
}
