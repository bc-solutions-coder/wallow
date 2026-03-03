using Foundry.Communications.Application.Channels.InApp.EventHandlers;
using Foundry.Communications.Application.Channels.InApp.Interfaces;
using Foundry.Communications.Domain.Channels.InApp.Enums;
using Foundry.Shared.Contracts.Communications.Announcements.Events;
using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Foundry.Communications.Tests.Application.Channels.InApp.EventHandlers;

public class AnnouncementPublishedEventHandlerTests
{
    private readonly INotificationService _notificationService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<AnnouncementPublishedEventHandler> _logger;
    private readonly TenantId _tenantId;

    public AnnouncementPublishedEventHandlerTests()
    {
        _notificationService = Substitute.For<INotificationService>();
        _tenantContext = Substitute.For<ITenantContext>();
        _logger = Substitute.For<ILogger<AnnouncementPublishedEventHandler>>();
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _tenantId = TenantId.Create(Guid.NewGuid());
        _tenantContext.TenantId.Returns(_tenantId);
    }

    [Fact]
    public async Task HandleAsync_WhenPinnedAndTargetAll_BroadcastsToTenant()
    {
        AnnouncementPublishedEvent evt = new()
        {
            AnnouncementId = Guid.NewGuid(),
            TenantId = _tenantId.Value,
            Title = "Important Update",
            Content = "Some content",
            Type = "Feature",
            Target = "All",
            TargetValue = null,
            IsPinned = true
        };

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            Arg.Any<string>(),
            Arg.Any<string>(),
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenAlertType_BroadcastsToTenant()
    {
        AnnouncementPublishedEvent evt = new()
        {
            AnnouncementId = Guid.NewGuid(),
            TenantId = _tenantId.Value,
            Title = "Alert",
            Content = "Alert content",
            Type = "Alert",
            Target = "All",
            TargetValue = null,
            IsPinned = false
        };

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            Arg.Is<string>(s => s.StartsWith("Important:")),
            Arg.Any<string>(),
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenNotPinnedAndNotAlert_SkipsBroadcast()
    {
        AnnouncementPublishedEvent evt = new()
        {
            AnnouncementId = Guid.NewGuid(),
            TenantId = _tenantId.Value,
            Title = "Update",
            Content = "Content",
            Type = "Feature",
            Target = "All",
            TargetValue = null,
            IsPinned = false
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
    public async Task HandleAsync_WhenTargetTenantAndMatchesTenant_BroadcastsToTenant()
    {
        AnnouncementPublishedEvent evt = new()
        {
            AnnouncementId = Guid.NewGuid(),
            TenantId = _tenantId.Value,
            Title = "Tenant Update",
            Content = "Content",
            Type = "Alert",
            Target = "Tenant",
            TargetValue = _tenantId.Value.ToString(),
            IsPinned = false
        };

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            Arg.Any<string>(),
            Arg.Any<string>(),
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenTargetTenantButDifferentTenant_DoesNotBroadcast()
    {
        AnnouncementPublishedEvent evt = new()
        {
            AnnouncementId = Guid.NewGuid(),
            TenantId = _tenantId.Value,
            Title = "Other Tenant",
            Content = "Content",
            Type = "Alert",
            Target = "Tenant",
            TargetValue = Guid.NewGuid().ToString(),
            IsPinned = false
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

    [Theory]
    [InlineData("Alert", "Important: Test")]
    [InlineData("Maintenance", "Maintenance Notice: Test")]
    [InlineData("Feature", "New Feature: Test")]
    [InlineData("Update", "Update: Test")]
    [InlineData("Other", "Test")]
    public async Task HandleAsync_FormatsNotificationTitleByType(string type, string expectedTitle)
    {
        AnnouncementPublishedEvent evt = new()
        {
            AnnouncementId = Guid.NewGuid(),
            TenantId = _tenantId.Value,
            Title = "Test",
            Content = "Content",
            Type = type,
            Target = "All",
            TargetValue = null,
            IsPinned = true
        };

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            expectedTitle,
            Arg.Any<string>(),
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_TruncatesLongContent()
    {
        string longContent = new('A', 300);
        AnnouncementPublishedEvent evt = new()
        {
            AnnouncementId = Guid.NewGuid(),
            TenantId = _tenantId.Value,
            Title = "Test",
            Content = longContent,
            Type = "Alert",
            Target = "All",
            TargetValue = null,
            IsPinned = true
        };

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            Arg.Any<string>(),
            Arg.Is<string>(s => s.Length <= 200 && s.EndsWith("...")),
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_StripsMarkdownFromContent()
    {
        string markdownContent = "## Header\n**Bold text** and [link](http://example.com)";
        AnnouncementPublishedEvent evt = new()
        {
            AnnouncementId = Guid.NewGuid(),
            TenantId = _tenantId.Value,
            Title = "Test",
            Content = markdownContent,
            Type = "Alert",
            Target = "All",
            TargetValue = null,
            IsPinned = true
        };

        await AnnouncementPublishedEventHandler.HandleAsync(
            evt, _notificationService,
            _tenantContext, _logger, CancellationToken.None);

        await _notificationService.Received(1).BroadcastToTenantAsync(
            _tenantId,
            Arg.Any<string>(),
            Arg.Is<string>(s => !s.Contains("##") && !s.Contains("**") && !s.Contains("[link]")),
            nameof(NotificationType.Announcement),
            Arg.Any<CancellationToken>());
    }
}
