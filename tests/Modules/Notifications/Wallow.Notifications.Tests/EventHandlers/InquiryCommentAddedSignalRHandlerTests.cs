using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Realtime;
using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Notifications.Tests.EventHandlers;

public class InquiryCommentAddedSignalRHandlerTests
{
    private readonly IRealtimeDispatcher _dispatcher = Substitute.For<IRealtimeDispatcher>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();

    private static readonly Guid _tenantId = Guid.NewGuid();

    private static InquiryCommentAddedEvent BuildEvent(bool isInternal = false) => new()
    {
        InquiryCommentId = Guid.NewGuid(),
        InquiryId = Guid.NewGuid(),
        TenantId = _tenantId,
        AuthorId = "admin-user-1",
        AuthorName = "Admin User",
        IsInternal = isInternal,
        SubmitterEmail = "submitter@test.com",
        SubmitterName = "Test Submitter",
        InquirySubject = "Test Inquiry",
        CommentContent = "Test comment"
    };

    [Fact]
    public async Task Handle_PublicComment_DispatchesToTenant()
    {
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantId));

        InquiryCommentAddedEvent @event = BuildEvent(isInternal: false);

        await InquiryCommentAddedSignalRHandler.Handle(@event, _tenantContext, _dispatcher);

        await _dispatcher.Received(1).SendToTenantAsync(
            _tenantId,
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Inquiries" &&
                e.Type == "InquiryCommentAdded"),
            Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToGroupAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_InternalComment_DispatchesToStaffGroupOnly()
    {
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId.Create(_tenantId));

        InquiryCommentAddedEvent @event = BuildEvent(isInternal: true);

        await InquiryCommentAddedSignalRHandler.Handle(@event, _tenantContext, _dispatcher);

        string expectedStaffGroup = $"tenant:{_tenantId}:staff";
        await _dispatcher.Received(1).SendToGroupAsync(
            expectedStaffGroup,
            Arg.Is<RealtimeEnvelope>(e =>
                e.Module == "Inquiries" &&
                e.Type == "InquiryCommentAdded"),
            Arg.Any<CancellationToken>());
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantAsync(default, default!, default);
    }

    [Fact]
    public async Task Handle_WhenTenantUnresolved_DoesNotDispatch()
    {
        _tenantContext.IsResolved.Returns(false);

        await InquiryCommentAddedSignalRHandler.Handle(BuildEvent(), _tenantContext, _dispatcher);

        await _dispatcher.DidNotReceiveWithAnyArgs().SendToTenantAsync(default, default!, default);
        await _dispatcher.DidNotReceiveWithAnyArgs().SendToGroupAsync(default!, default!, default);
    }
}
