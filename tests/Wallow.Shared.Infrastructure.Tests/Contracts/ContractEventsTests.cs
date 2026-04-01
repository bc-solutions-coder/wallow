using Wallow.Shared.Contracts.Announcements.Events;
using Wallow.Shared.Contracts.Delivery.Events;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Contracts.Notifications.Events;
using Wallow.Shared.Contracts.Storage;
using Wallow.Shared.Contracts.Storage.Commands;

namespace Wallow.Shared.Infrastructure.Tests.Contracts;

public class ContractEventsTests
{
    // ── Delivery events ──────────────────────────────────────────────────

    [Fact]
    public void EmailSentEvent_WithAllProperties_HasCorrectValues()
    {
        Guid emailId = Guid.NewGuid();

        EmailSentEvent evt = new()
        {
            EmailId = emailId,
            TenantId = Guid.NewGuid(),
            ToAddress = "recipient@example.com",
            Subject = "Welcome!",
            TemplateName = "welcome-email",
            SentAt = DateTime.UtcNow
        };

        evt.EmailId.Should().Be(emailId);
        evt.ToAddress.Should().Be("recipient@example.com");
        evt.Subject.Should().Be("Welcome!");
        evt.TemplateName.Should().Be("welcome-email");
    }

    [Fact]
    public void SmsSentEvent_WithAllProperties_HasCorrectValues()
    {
        Guid smsId = Guid.NewGuid();

        SmsSentEvent evt = new()
        {
            SmsId = smsId,
            TenantId = Guid.NewGuid(),
            ToNumber = "+1-555-0100",
            SentAt = DateTime.UtcNow
        };

        evt.SmsId.Should().Be(smsId);
        evt.ToNumber.Should().Be("+1-555-0100");
    }

    [Fact]
    public void PushSentEvent_WithAllProperties_HasCorrectValues()
    {
        Guid pushId = Guid.NewGuid();
        Guid recipientId = Guid.NewGuid();

        PushSentEvent evt = new()
        {
            PushId = pushId,
            TenantId = Guid.NewGuid(),
            RecipientId = recipientId,
            SentAt = DateTime.UtcNow
        };

        evt.PushId.Should().Be(pushId);
        evt.RecipientId.Should().Be(recipientId);
    }

    // ── Identity events ──────────────────────────────────────────────────

    [Fact]
    public void OrganizationCreatedEvent_WithAllProperties_HasCorrectValues()
    {
        Guid orgId = Guid.NewGuid();

        OrganizationCreatedEvent evt = new()
        {
            OrganizationId = orgId,
            TenantId = Guid.NewGuid(),
            Name = "ACME Inc.",
            Domain = "acme.com",
            CreatorEmail = "founder@acme.com"
        };

        evt.OrganizationId.Should().Be(orgId);
        evt.Name.Should().Be("ACME Inc.");
        evt.Domain.Should().Be("acme.com");
        evt.CreatorEmail.Should().Be("founder@acme.com");
    }

    [Fact]
    public void OrganizationCreatedEvent_Domain_IsOptional()
    {
        OrganizationCreatedEvent evt = new()
        {
            OrganizationId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Org",
            CreatorEmail = "e@example.com"
        };

        evt.Domain.Should().BeNull();
    }

    [Fact]
    public void OrganizationMemberAddedEvent_WithAllProperties_HasCorrectValues()
    {
        Guid orgId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        OrganizationMemberAddedEvent evt = new()
        {
            OrganizationId = orgId,
            TenantId = Guid.NewGuid(),
            UserId = userId,
            Email = "member@example.com"
        };

        evt.OrganizationId.Should().Be(orgId);
        evt.UserId.Should().Be(userId);
        evt.Email.Should().Be("member@example.com");
    }

    [Fact]
    public void OrganizationMemberRemovedEvent_WithAllProperties_HasCorrectValues()
    {
        OrganizationMemberRemovedEvent evt = new()
        {
            OrganizationId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Email = "removed@example.com"
        };

        evt.Email.Should().Be("removed@example.com");
    }

    [Fact]
    public void PasswordResetRequestedEvent_WithAllProperties_HasCorrectValues()
    {
        Guid userId = Guid.NewGuid();

        PasswordResetRequestedEvent evt = new()
        {
            UserId = userId,
            TenantId = Guid.NewGuid(),
            Email = "reset@example.com",
            ResetToken = "token-abc-123",
            ResetUrl = "http://localhost/reset?token=token-abc-123"
        };

        evt.UserId.Should().Be(userId);
        evt.Email.Should().Be("reset@example.com");
        evt.ResetToken.Should().Be("token-abc-123");
    }

    [Fact]
    public void UserRoleChangedEvent_WithAllProperties_HasCorrectValues()
    {
        UserRoleChangedEvent evt = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@example.com",
            OldRole = "Viewer",
            NewRole = "Admin"
        };

        evt.OldRole.Should().Be("Viewer");
        evt.NewRole.Should().Be("Admin");
    }

    // ── Notifications events ─────────────────────────────────────────────

    [Fact]
    public void NotificationCreatedEvent_WithAllProperties_HasCorrectValues()
    {
        Guid notificationId = Guid.NewGuid();

        NotificationCreatedEvent evt = new()
        {
            NotificationId = notificationId,
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Title = "New message",
            Type = "info",
            CreatedAt = DateTime.UtcNow
        };

        evt.NotificationId.Should().Be(notificationId);
        evt.Title.Should().Be("New message");
        evt.Type.Should().Be("info");
    }

    // ── Inquiries events ─────────────────────────────────────────────────

    [Fact]
    public void InquirySubmittedEvent_WithAllProperties_HasCorrectValues()
    {
        Guid inquiryId = Guid.NewGuid();

        InquirySubmittedEvent evt = new()
        {
            InquiryId = inquiryId,
            Name = "Jane Doe",
            Email = "jane@example.com",
            Company = "Acme",
            Phone = "+1-555-0200",
            ProjectType = "Question",
            Message = "Hello, I have a question",
            SubmittedAt = DateTime.UtcNow,
            AdminEmail = "admin@wallow.dev",
            AdminUserIds = []
        };

        evt.InquiryId.Should().Be(inquiryId);
        evt.Name.Should().Be("Jane Doe");
        evt.Company.Should().Be("Acme");
        evt.Phone.Should().Be("+1-555-0200");
    }

    [Fact]
    public void InquirySubmittedEvent_OptionalFields_CanBeNull()
    {
        InquirySubmittedEvent evt = new()
        {
            InquiryId = Guid.NewGuid(),
            Name = "John",
            Email = "john@example.com",
            Phone = "+1-555-0100",
            ProjectType = "Subject",
            Message = "Message",
            SubmittedAt = DateTime.UtcNow,
            AdminEmail = "admin@wallow.dev",
            AdminUserIds = []
        };

        evt.Company.Should().BeNull();
    }

    [Fact]
    public void InquiryStatusChangedEvent_WithAllProperties_HasCorrectValues()
    {
        InquiryStatusChangedEvent evt = new()
        {
            InquiryId = Guid.NewGuid(),
            OldStatus = "Pending",
            NewStatus = "InProgress",
            ChangedAt = DateTime.UtcNow,
            SubmitterEmail = "user@example.com"
        };

        evt.OldStatus.Should().Be("Pending");
        evt.NewStatus.Should().Be("InProgress");
        evt.SubmitterEmail.Should().Be("user@example.com");
    }

    // ── Announcements events ─────────────────────────────────────────────

    [Fact]
    public void AnnouncementPublishedEvent_WithAllProperties_HasCorrectValues()
    {
        Guid announcementId = Guid.NewGuid();
        List<Guid> targetUsers = [Guid.NewGuid(), Guid.NewGuid()];

        AnnouncementPublishedEvent evt = new()
        {
            AnnouncementId = announcementId,
            TenantId = Guid.NewGuid(),
            Title = "System Maintenance",
            Content = "Scheduled downtime tonight",
            Type = "warning",
            Target = "all",
            TargetValue = null,
            IsPinned = true,
            TargetUserIds = targetUsers
        };

        evt.AnnouncementId.Should().Be(announcementId);
        evt.Title.Should().Be("System Maintenance");
        evt.IsPinned.Should().BeTrue();
        evt.TargetUserIds.Should().HaveCount(2);
    }

    // ── Storage contracts ────────────────────────────────────────────────

    [Fact]
    public void UploadFileCommand_Constructor_SetsAllProperties()
    {
        Guid tenantId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        Stream content = Stream.Null;

        UploadFileCommand cmd = new(
            TenantId: tenantId,
            UserId: userId,
            BucketName: "documents",
            FileName: "report.pdf",
            ContentType: "application/pdf",
            Content: content,
            SizeBytes: 1024L,
            Path: "reports/2026",
            IsPublic: false,
            Metadata: null);

        cmd.TenantId.Should().Be(tenantId);
        cmd.UserId.Should().Be(userId);
        cmd.BucketName.Should().Be("documents");
        cmd.FileName.Should().Be("report.pdf");
        cmd.ContentType.Should().Be("application/pdf");
        cmd.SizeBytes.Should().Be(1024L);
        cmd.Path.Should().Be("reports/2026");
        cmd.IsPublic.Should().BeFalse();
    }

    [Fact]
    public void UploadFileCommand_OptionalFields_HaveDefaults()
    {
        UploadFileCommand cmd = new(
            TenantId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            BucketName: "bucket",
            FileName: "file.txt",
            ContentType: "text/plain",
            Content: Stream.Null,
            SizeBytes: 100L);

        cmd.Path.Should().BeNull();
        cmd.IsPublic.Should().BeFalse();
        cmd.Metadata.Should().BeNull();
    }

    [Fact]
    public void UploadResult_Constructor_SetsAllProperties()
    {
        Guid fileId = Guid.NewGuid();
        DateTime uploadedAt = DateTime.UtcNow;

        UploadResult result = new(
            FileId: fileId,
            FileName: "doc.pdf",
            StorageKey: "tenant/docs/doc.pdf",
            SizeBytes: 2048L,
            ContentType: "application/pdf",
            UploadedAt: uploadedAt);

        result.FileId.Should().Be(fileId);
        result.FileName.Should().Be("doc.pdf");
        result.StorageKey.Should().Be("tenant/docs/doc.pdf");
        result.SizeBytes.Should().Be(2048L);
        result.ContentType.Should().Be("application/pdf");
        result.UploadedAt.Should().Be(uploadedAt);
    }

}
