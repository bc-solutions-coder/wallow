using Foundry.Communications.Application.Channels.InApp.EventHandlers;
using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Domain.Channels.InApp.Enums;
using Foundry.Shared.Contracts.Communications.Announcements.Events;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Tests.Application.Channels.InApp.EventHandlers;

public class AnnouncementPublishedMarkdownTests
{
    private readonly INotificationService _notificationService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AnnouncementPublishedEventHandler> _logger;
    private readonly TenantId _tenantId;

    public AnnouncementPublishedMarkdownTests()
    {
        _notificationService = Substitute.For<INotificationService>();
        _tenantContext = Substitute.For<ITenantContext>();
        _logger = Substitute.For<ILogger<AnnouncementPublishedEventHandler>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _tenantId = TenantId.Create(Guid.NewGuid());
        _tenantContext.TenantId.Returns(_tenantId);
    }

    [Fact]
    public async Task HandleAsync_WithEmptyContent_BroadcastsEmptyMessage()
    {
        AnnouncementPublishedEvent evt = CreateEvent(content: "", isPinned: true);

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            Arg.Any<string>(),
            Arg.Is<string>(s => s == string.Empty),
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithUnderscoreMarkdown_StripsUnderscoreFormatting()
    {
        AnnouncementPublishedEvent evt = CreateEvent(
            content: "This is __bold__ and _italic_ text", isPinned: true);

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            Arg.Any<string>(),
            Arg.Is<string>(s => !s.Contains("__") && !s.Contains("_italic_", StringComparison.Ordinal)),
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithCodeBlocks_StripsCodeBlocks()
    {
        string content = "Before\n```csharp\nvar x = 1;\n```\nAfter";
        AnnouncementPublishedEvent evt = CreateEvent(content: content, isPinned: true);

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            Arg.Any<string>(),
            Arg.Is<string>(s => !s.Contains("```") && s.Contains("Before") && s.Contains("After")),
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithInlineCode_StripsInlineCode()
    {
        AnnouncementPublishedEvent evt = CreateEvent(
            content: "Use `dotnet build` to compile", isPinned: true);

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            Arg.Any<string>(),
            Arg.Is<string>(s => !s.Contains('`')),
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithContentExactlyAtMaxLength_DoesNotTruncate()
    {
        string content = new('A', 200);
        AnnouncementPublishedEvent evt = CreateEvent(content: content, isPinned: true);

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Length == 200 && !s.EndsWith("...")),
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithNullContent_BroadcastsEmptyMessage()
    {
        AnnouncementPublishedEvent evt = CreateEvent(content: null!, isPinned: true);

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            Arg.Any<string>(),
            Arg.Is<string>(s => s == string.Empty),
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithTargetRole_DoesNotBroadcast()
    {
        AnnouncementPublishedEvent evt = new()
        {
            AnnouncementId = Guid.NewGuid(),
            TenantId = _tenantId.Value,
            Title = "For Admins Only",
            Content = "Content",
            Type = "Alert",
            Target = "Role",
            TargetValue = "Admin",
            IsPinned = true
        };

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.DidNotReceive().BroadcastToTenantAsync(
            Arg.Any<TenantId>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithShortContent_DoesNotTruncate()
    {
        AnnouncementPublishedEvent evt = CreateEvent(content: "Short", isPinned: true);

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            Arg.Any<string>(),
            "Short",
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithLinksInContent_KeepsLinkText()
    {
        AnnouncementPublishedEvent evt = CreateEvent(
            content: "Check [our docs](https://example.com/docs) for details", isPinned: true);

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Contains("our docs") && !s.Contains("https://example.com")),
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        AnnouncementPublishedEvent evt = CreateEvent(isPinned: true);

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, cts.Token);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            Arg.Any<TenantId>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            cts.Token);
    }

    private AnnouncementPublishedEvent CreateEvent(
        string content = "Content",
        bool isPinned = false,
        string type = "Alert",
        string target = "All")
    {
        return new AnnouncementPublishedEvent
        {
            AnnouncementId = Guid.NewGuid(),
            TenantId = _tenantId.Value,
            Title = "Test",
            Content = content,
            Type = type,
            Target = target,
            TargetValue = null,
            IsPinned = isPinned
        };
    }
}
