using Wallow.Notifications.Application.Channels.InApp.Commands.SendNotification;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Notifications.Domain.Enums;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InquirySubmittedInAppHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task Handle_FansOutInAppNotificationToAllAdminUserIds()
    {
        Guid adminId1 = Guid.NewGuid();
        Guid adminId2 = Guid.NewGuid();

        InquirySubmittedEvent @event = new()
        {
            InquiryId = Guid.NewGuid(),
            Name = "Jane Doe",
            Email = "jane@test.com",
            Phone = "555-0100",
            ProjectType = "Sales Question",
            Message = "I have a question about pricing.",
            SubmittedAt = DateTime.UtcNow,
            AdminEmail = "admin@company.com",
            AdminUserIds = [adminId1, adminId2]
        };

        await InquirySubmittedInAppHandler.Handle(@event, _bus);

        await _bus.Received(2).InvokeAsync(
            Arg.Is<SendNotificationCommand>(cmd =>
                cmd.Type == NotificationType.InquirySubmitted),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendNotificationCommand>(cmd => cmd.UserId == adminId1),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendNotificationCommand>(cmd => cmd.UserId == adminId2),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenAdminUserIdsIsEmpty()
    {
        InquirySubmittedEvent @event = new()
        {
            InquiryId = Guid.NewGuid(),
            Name = "Jane Doe",
            Email = "jane@test.com",
            Phone = "555-0100",
            ProjectType = "Sales Question",
            Message = "I have a question about pricing.",
            SubmittedAt = DateTime.UtcNow,
            AdminEmail = "admin@company.com",
            AdminUserIds = []
        };

        await InquirySubmittedInAppHandler.Handle(@event, _bus);

        await _bus.DidNotReceive().InvokeAsync(
            Arg.Any<SendNotificationCommand>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_SendsCorrectTitleAndMessage()
    {
        Guid adminId = Guid.NewGuid();

        InquirySubmittedEvent @event = new()
        {
            InquiryId = Guid.NewGuid(),
            Name = "John Smith",
            Email = "john@example.com",
            Phone = "555-0200",
            ProjectType = "Website Redesign",
            Message = "Looking for a quote.",
            SubmittedAt = DateTime.UtcNow,
            AdminEmail = "admin@company.com",
            AdminUserIds = [adminId]
        };

        await InquirySubmittedInAppHandler.Handle(@event, _bus);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendNotificationCommand>(cmd =>
                cmd.Title.Contains("Website Redesign") &&
                cmd.Message.Contains("John Smith") &&
                cmd.Message.Contains("john@example.com")),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
