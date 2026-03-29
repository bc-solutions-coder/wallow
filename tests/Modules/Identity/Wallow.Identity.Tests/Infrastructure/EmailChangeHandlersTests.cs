using System.Reflection;
using Microsoft.Extensions.Logging;
using Wallow.Identity.Infrastructure.Handlers;
using Wallow.Shared.Contracts.Identity.Events;

namespace Wallow.Identity.Tests.Infrastructure;

public class EmailChangeHandlersTests
{
    [Fact]
    public void Handler_is_a_static_class()
    {
        Type handlerType = typeof(EmailChangeHandlers);

        handlerType.IsAbstract.Should().BeTrue();
        handlerType.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Handle_UserEmailChangeRequestedEvent_is_static_method()
    {
        MethodInfo? handleMethod = typeof(EmailChangeHandlers)
            .GetMethod("Handle", [typeof(UserEmailChangeRequestedEvent), typeof(ILogger)]);

        handleMethod.Should().NotBeNull();
        handleMethod!.IsStatic.Should().BeTrue();
    }

    [Fact]
    public void Handle_UserEmailChangedEvent_is_static_method()
    {
        MethodInfo? handleMethod = typeof(EmailChangeHandlers)
            .GetMethod("Handle", [typeof(UserEmailChangedEvent), typeof(ILogger)]);

        handleMethod.Should().NotBeNull();
        handleMethod!.IsStatic.Should().BeTrue();
    }

    [Fact]
    public void Handle_UserEmailChangeRequestedEvent_logs_with_correct_arguments()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        string newEmail = "new@example.com";
        string confirmationUrl = "https://example.com/confirm?token=abc";

        UserEmailChangeRequestedEvent evt = new()
        {
            UserId = userId,
            TenantId = tenantId,
            NewEmail = newEmail,
            ConfirmationUrl = confirmationUrl,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        TestLogger logger = new();

        EmailChangeHandlers.Handle(evt, logger);

        logger.Entries.Should().HaveCount(1);
        logger.Entries[0].LogLevel.Should().Be(LogLevel.Information);
        logger.Entries[0].Message.Should().Contain(userId.ToString());
        logger.Entries[0].Message.Should().Contain(tenantId.ToString());
        logger.Entries[0].Message.Should().Contain(newEmail);
    }

    [Fact]
    public void Handle_UserEmailChangedEvent_logs_old_and_new_email()
    {
        Guid userId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        string oldEmail = "old@example.com";
        string newEmail = "new@example.com";

        UserEmailChangedEvent evt = new()
        {
            UserId = userId,
            TenantId = tenantId,
            OldEmail = oldEmail,
            NewEmail = newEmail
        };

        TestLogger logger = new();

        EmailChangeHandlers.Handle(evt, logger);

        logger.Entries.Should().HaveCount(1);
        logger.Entries[0].LogLevel.Should().Be(LogLevel.Information);
        logger.Entries[0].Message.Should().Contain(oldEmail);
        logger.Entries[0].Message.Should().Contain(newEmail);
        logger.Entries[0].Message.Should().Contain(userId.ToString());
        logger.Entries[0].Message.Should().Contain(tenantId.ToString());
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
