using Foundry.Communications.Application.Channels.InApp.EventHandlers;
using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Domain.Channels.InApp.Entities;
using Foundry.Communications.Domain.Enums;
using Foundry.Shared.Contracts.Identity.Events;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Tests.Application.Channels.InApp.EventHandlers;

public class UserRegisteredInAppEventHandlerTests
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationService _notificationService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UserRegisteredEventHandler> _logger;

    public UserRegisteredInAppEventHandlerTests()
    {
        _notificationRepository = Substitute.For<INotificationRepository>();
        _notificationService = Substitute.For<INotificationService>();
        _tenantContext = Substitute.For<ITenantContext>();
        _tenantContext.TenantId.Returns(TenantId.Create(Guid.NewGuid()));
        _logger = Substitute.For<ILogger<UserRegisteredEventHandler>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    [Fact]
    public async Task HandleAsync_CreatesWelcomeNotification()
    {
        Guid userId = Guid.NewGuid();
        UserRegisteredEvent evt = new()
        {
            UserId = userId,
            TenantId = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe"
        };

        await UserRegisteredEventHandler.HandleAsync(
            evt, _notificationRepository, _notificationService,
            _tenantContext, TimeProvider.System, _logger, CancellationToken.None);

        _notificationRepository.Received(1).Add(Arg.Is<Notification>(n =>
            n.UserId == userId &&
            n.Type == NotificationType.SystemAlert &&
            n.Title == "Welcome to Foundry!"));
        await _notificationRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SendsRealtimeNotification()
    {
        Guid userId = Guid.NewGuid();
        UserRegisteredEvent evt = new()
        {
            UserId = userId,
            TenantId = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "Jane",
            LastName = "Doe"
        };

        await UserRegisteredEventHandler.HandleAsync(
            evt, _notificationRepository, _notificationService,
            _tenantContext, TimeProvider.System, _logger, CancellationToken.None);

        await _notificationService.Received(1).SendToUserAsync(
            userId,
            "Welcome to Foundry!",
            Arg.Is<string>(s => s.Contains("Jane")),
            nameof(NotificationType.SystemAlert),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_IncludesFirstNameInMessage()
    {
        UserRegisteredEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "Alice",
            LastName = "Smith"
        };

        await UserRegisteredEventHandler.HandleAsync(
            evt, _notificationRepository, _notificationService,
            _tenantContext, TimeProvider.System, _logger, CancellationToken.None);

        await _notificationService.Received(1).SendToUserAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("Alice")),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        UserRegisteredEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User"
        };

        await UserRegisteredEventHandler.HandleAsync(
            evt, _notificationRepository, _notificationService,
            _tenantContext, TimeProvider.System, _logger, cts.Token);

        await _notificationRepository.Received(1).SaveChangesAsync(cts.Token);
        await _notificationService.Received(1).SendToUserAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            cts.Token);
    }
}
