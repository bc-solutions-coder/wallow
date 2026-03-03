using Foundry.Communications.Application.Channels.Email.EventHandlers;
using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Enums;
using Foundry.Shared.Contracts.Communications.Email;
using Foundry.Shared.Contracts.Identity.Events;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Tests.Application.Channels.Email.EventHandlers;

public class PasswordResetRequestedEventHandlerTests
{
    private readonly IEmailPreferenceRepository _preferenceRepository;
    private readonly IEmailTemplateService _templateService;
    private readonly IEmailService _emailService;
    private readonly ILogger<PasswordResetRequestedEventHandler> _logger;

    public PasswordResetRequestedEventHandlerTests()
    {
        _preferenceRepository = Substitute.For<IEmailPreferenceRepository>();
        _templateService = Substitute.For<IEmailTemplateService>();
        _emailService = Substitute.For<IEmailService>();
        _logger = Substitute.For<ILogger<PasswordResetRequestedEventHandler>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    [Fact]
    public async Task HandleAsync_WhenNoPreference_SendsPasswordResetEmail()
    {
        Guid userId = Guid.NewGuid();
        _preferenceRepository.GetByUserAndTypeAsync(userId, NotificationType.SystemNotification, Arg.Any<CancellationToken>())
            .Returns((EmailPreference?)null);
        _templateService.RenderAsync("PasswordReset", Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("Reset HTML body");

        PasswordResetRequestedEvent evt = new()
        {
            UserId = userId,
            TenantId = Guid.NewGuid(),
            Email = "user@example.com",
            ResetToken = "reset-token-123"
        };

        await PasswordResetRequestedEventHandler.HandleAsync(
            evt, _preferenceRepository, _templateService, _emailService, _logger, CancellationToken.None);

        await _emailService.Received(1).SendAsync(
            "user@example.com",
            null,
            "Password Reset Request",
            "Reset HTML body",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenPreferenceDisabled_SkipsEmail()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference preference = EmailPreference.Create(userId, NotificationType.SystemNotification, false);

        _preferenceRepository.GetByUserAndTypeAsync(userId, NotificationType.SystemNotification, Arg.Any<CancellationToken>())
            .Returns(preference);

        PasswordResetRequestedEvent evt = new()
        {
            UserId = userId,
            TenantId = Guid.NewGuid(),
            Email = "user@example.com",
            ResetToken = "token"
        };

        await PasswordResetRequestedEventHandler.HandleAsync(
            evt, _preferenceRepository, _templateService, _emailService, _logger, CancellationToken.None);

        await _emailService.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenPreferenceEnabled_SendsEmail()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference preference = EmailPreference.Create(userId, NotificationType.SystemNotification, true);

        _preferenceRepository.GetByUserAndTypeAsync(userId, NotificationType.SystemNotification, Arg.Any<CancellationToken>())
            .Returns(preference);
        _templateService.RenderAsync("PasswordReset", Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("Reset body");

        PasswordResetRequestedEvent evt = new()
        {
            UserId = userId,
            TenantId = Guid.NewGuid(),
            Email = "user@example.com",
            ResetToken = "token"
        };

        await PasswordResetRequestedEventHandler.HandleAsync(
            evt, _preferenceRepository, _templateService, _emailService, _logger, CancellationToken.None);

        await _emailService.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RendersPasswordResetTemplate()
    {
        Guid userId = Guid.NewGuid();
        _preferenceRepository.GetByUserAndTypeAsync(userId, NotificationType.SystemNotification, Arg.Any<CancellationToken>())
            .Returns((EmailPreference?)null);
        _templateService.RenderAsync("PasswordReset", Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("body");

        PasswordResetRequestedEvent evt = new()
        {
            UserId = userId,
            TenantId = Guid.NewGuid(),
            Email = "user@example.com",
            ResetToken = "token"
        };

        await PasswordResetRequestedEventHandler.HandleAsync(
            evt, _preferenceRepository, _templateService, _emailService, _logger, CancellationToken.None);

        await _templateService.Received(1).RenderAsync("PasswordReset", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        Guid userId = Guid.NewGuid();
        _preferenceRepository.GetByUserAndTypeAsync(userId, NotificationType.SystemNotification, Arg.Any<CancellationToken>())
            .Returns((EmailPreference?)null);
        _templateService.RenderAsync("PasswordReset", Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("body");

        PasswordResetRequestedEvent evt = new()
        {
            UserId = userId,
            TenantId = Guid.NewGuid(),
            Email = "user@example.com",
            ResetToken = "token"
        };

        await PasswordResetRequestedEventHandler.HandleAsync(
            evt, _preferenceRepository, _templateService, _emailService, _logger, cts.Token);

        await _preferenceRepository.Received(1).GetByUserAndTypeAsync(userId, NotificationType.SystemNotification, cts.Token);
        await _emailService.Received(1).SendAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(),
            Arg.Any<string>(), cts.Token);
    }
}
