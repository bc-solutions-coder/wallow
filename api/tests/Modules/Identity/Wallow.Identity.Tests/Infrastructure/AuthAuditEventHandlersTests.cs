using Wallow.Identity.Infrastructure.Handlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Auditing;

namespace Wallow.Identity.Tests.Infrastructure;

public class AuthAuditEventHandlersTests
{
    private readonly IAuthAuditService _auditService = Substitute.For<IAuthAuditService>();

    [Fact]
    public async Task Handle_UserLoginSucceededEvent_RecordsAudit()
    {
        UserLoginSucceededEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IpAddress = "10.0.0.1"
        };

        await AuthAuditEventHandlers.Handle(evt, _auditService);

        await _auditService.Received(1).RecordAsync(
            Arg.Is<AuthAuditRecord>(r =>
                r.EventType == "LoginSucceeded" &&
                r.UserId == evt.UserId &&
                r.TenantId == evt.TenantId &&
                r.IpAddress == "10.0.0.1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserLoginFailedEvent_RecordsAudit()
    {
        UserLoginFailedEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IpAddress = "10.0.0.2",
            Reason = "InvalidPassword"
        };

        await AuthAuditEventHandlers.Handle(evt, _auditService);

        await _auditService.Received(1).RecordAsync(
            Arg.Is<AuthAuditRecord>(r =>
                r.EventType == "LoginFailed" &&
                r.UserId == evt.UserId &&
                r.TenantId == evt.TenantId &&
                r.IpAddress == "10.0.0.2"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserAccountLockedOutEvent_RecordsAudit()
    {
        UserAccountLockedOutEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IpAddress = "10.0.0.3"
        };

        await AuthAuditEventHandlers.Handle(evt, _auditService);

        await _auditService.Received(1).RecordAsync(
            Arg.Is<AuthAuditRecord>(r =>
                r.EventType == "AccountLockedOut" &&
                r.UserId == evt.UserId &&
                r.TenantId == evt.TenantId &&
                r.IpAddress == "10.0.0.3"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserMfaLockedOutEvent_RecordsAudit()
    {
        UserMfaLockedOutEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LockoutCount = 5
        };

        await AuthAuditEventHandlers.Handle(evt, _auditService);

        await _auditService.Received(1).RecordAsync(
            Arg.Is<AuthAuditRecord>(r =>
                r.EventType == "MfaLockedOut" &&
                r.UserId == evt.UserId &&
                r.TenantId == evt.TenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserLoginSucceededEvent_WithNullIpAddress_RecordsAudit()
    {
        UserLoginSucceededEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IpAddress = null
        };

        await AuthAuditEventHandlers.Handle(evt, _auditService);

        await _auditService.Received(1).RecordAsync(
            Arg.Is<AuthAuditRecord>(r =>
                r.IpAddress == null),
            Arg.Any<CancellationToken>());
    }
}
