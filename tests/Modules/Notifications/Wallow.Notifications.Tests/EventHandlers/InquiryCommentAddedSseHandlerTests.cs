using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InquiryCommentAddedSseHandlerTests
{
    private readonly ISseDispatcher _dispatcher = Substitute.For<ISseDispatcher>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private static readonly Guid _tenantId = Guid.NewGuid();
    private const string StaffUserId = "staff-user-1";
    private static readonly Guid _submitterUserId = Guid.NewGuid();

    private static InquiryCommentAddedEvent BuildEvent(
        bool isInternal = false,
        string? authorId = null,
        Guid? submitterUserId = null)
    {
        return new InquiryCommentAddedEvent
        {
            InquiryCommentId = Guid.NewGuid(),
            InquiryId = Guid.NewGuid(),
            TenantId = _tenantId,
            AuthorId = authorId ?? StaffUserId,
            AuthorName = "Staff User",
            IsInternal = isInternal,
            SubmitterEmail = "submitter@test.com",
            SubmitterName = "Test Submitter",
            SubmitterUserId = submitterUserId ?? _submitterUserId,
            InquirySubject = "Test Inquiry",
            CommentContent = "Test comment",
        };
    }

    [Fact]
    public async Task Handle_InternalComment_SendsToStaffWithPermission()
    {
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantId));

        InquiryCommentAddedEvent @event = BuildEvent(isInternal: true);

        await InquiryCommentAddedSseHandler.Handle(@event, _tenantContext, _dispatcher);

        await _dispatcher.Received(1).SendToTenantPermissionAsync(
            _tenantId,
            "inquiries.manage",
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Inquiries" &&
                e.Type == "InquiryCommentAdded"),
            Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToUserAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_StaffComment_SendsToSubmitter()
    {
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantId));

        InquiryCommentAddedEvent @event = BuildEvent(
            authorId: StaffUserId,
            submitterUserId: _submitterUserId);

        await InquiryCommentAddedSseHandler.Handle(@event, _tenantContext, _dispatcher);

        await _dispatcher.Received(1).SendToUserAsync(
            _submitterUserId.ToString(),
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Inquiries" &&
                e.Type == "InquiryCommentAdded"),
            Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantAsync(default, default!, default);
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantPermissionAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task Handle_SubmitterComment_SendsToStaffWithPermission()
    {
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantId));

        InquiryCommentAddedEvent @event = BuildEvent(
            authorId: _submitterUserId.ToString(),
            submitterUserId: _submitterUserId);

        await InquiryCommentAddedSseHandler.Handle(@event, _tenantContext, _dispatcher);

        await _dispatcher.Received(1).SendToTenantPermissionAsync(
            _tenantId,
            "inquiries.manage",
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Inquiries" &&
                e.Type == "InquiryCommentAdded"),
            Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToUserAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_StaffComment_NoSubmitterUserId_DoesNotDispatch()
    {
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantId));

        InquiryCommentAddedEvent @event = BuildEvent(authorId: StaffUserId) with
        {
            SubmitterUserId = null,
        };

        await InquiryCommentAddedSseHandler.Handle(@event, _tenantContext, _dispatcher);

        await _dispatcher.DidNotReceiveWithAnyArgs().SendToUserAsync(default!, default!, default);
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantAsync(default, default!, default);
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantPermissionAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task Handle_TenantUnresolved_DoesNotDispatch()
    {
        _tenantContext.IsResolved.Returns(false);

        await InquiryCommentAddedSseHandler.Handle(BuildEvent(), _tenantContext, _dispatcher);

        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantAsync(default, default!, default);
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantPermissionAsync(default, default!, default!, default);
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToUserAsync(default!, default!, default);
    }
}
