using Foundry.Shared.Contracts.Announcements.Events;
using Foundry.Shared.Contracts.Billing;
using Foundry.Shared.Contracts.Billing.Events;
using Foundry.Shared.Contracts.Delivery.Events;
using Foundry.Shared.Contracts.Identity.Events;
using Foundry.Shared.Contracts.Inquiries.Events;
using Foundry.Shared.Contracts.Messaging.Events;
using Foundry.Shared.Contracts.Metering;
using Foundry.Shared.Contracts.Metering.Events;
using Foundry.Shared.Contracts.Notifications.Events;
using Foundry.Shared.Contracts.Storage;
using Foundry.Shared.Contracts.Storage.Commands;

namespace Foundry.Shared.Infrastructure.Tests.Contracts;

public class ContractEventsTests
{
    // ── Billing events ───────────────────────────────────────────────────

    [Fact]
    public void InvoiceOverdueEvent_WithAllProperties_HasCorrectValues()
    {
        Guid invoiceId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        DateTime dueDate = DateTime.UtcNow.AddDays(-5);

        InvoiceOverdueEvent evt = new()
        {
            InvoiceId = invoiceId,
            TenantId = tenantId,
            UserId = Guid.NewGuid(),
            UserEmail = "overdue@example.com",
            InvoiceNumber = "INV-002",
            Amount = 150m,
            Currency = "EUR",
            DueDate = dueDate
        };

        evt.InvoiceId.Should().Be(invoiceId);
        evt.TenantId.Should().Be(tenantId);
        evt.UserEmail.Should().Be("overdue@example.com");
        evt.Amount.Should().Be(150m);
        evt.Currency.Should().Be("EUR");
    }

    [Fact]
    public void InvoicePaidEvent_WithAllProperties_HasCorrectValues()
    {
        Guid invoiceId = Guid.NewGuid();
        Guid paymentId = Guid.NewGuid();

        InvoicePaidEvent evt = new()
        {
            InvoiceId = invoiceId,
            TenantId = Guid.NewGuid(),
            PaymentId = paymentId,
            UserId = Guid.NewGuid(),
            UserEmail = "paid@example.com",
            InvoiceNumber = "INV-003",
            Amount = 200m,
            Currency = "GBP",
            PaidAt = DateTime.UtcNow
        };

        evt.InvoiceId.Should().Be(invoiceId);
        evt.PaymentId.Should().Be(paymentId);
        evt.Currency.Should().Be("GBP");
        evt.PaidAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PaymentReceivedEvent_WithAllProperties_HasCorrectValues()
    {
        Guid paymentId = Guid.NewGuid();

        PaymentReceivedEvent evt = new()
        {
            PaymentId = paymentId,
            TenantId = Guid.NewGuid(),
            InvoiceId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserEmail = "user@example.com",
            Amount = 300m,
            Currency = "USD",
            PaymentMethod = "card",
            PaidAt = DateTime.UtcNow
        };

        evt.PaymentId.Should().Be(paymentId);
        evt.Amount.Should().Be(300m);
        evt.PaymentMethod.Should().Be("card");
    }

    // ── Billing report rows ──────────────────────────────────────────────

    [Fact]
    public void InvoiceReportRow_Constructor_SetsAllProperties()
    {
        DateTime issueDate = DateTime.UtcNow;
        DateTime dueDate = issueDate.AddDays(30);

        InvoiceReportRow row = new("INV-001", "ACME Corp", 500m, "USD", "Paid", issueDate, dueDate);

        row.InvoiceNumber.Should().Be("INV-001");
        row.CustomerName.Should().Be("ACME Corp");
        row.Amount.Should().Be(500m);
        row.Currency.Should().Be("USD");
        row.Status.Should().Be("Paid");
        row.IssueDate.Should().Be(issueDate);
        row.DueDate.Should().Be(dueDate);
    }

    [Fact]
    public void InvoiceReportRow_DueDate_CanBeNull()
    {
        InvoiceReportRow row = new("INV-002", "Customer", 100m, "USD", "Draft", DateTime.UtcNow, null);

        row.DueDate.Should().BeNull();
    }

    [Fact]
    public void RevenueReportRow_Constructor_SetsAllProperties()
    {
        RevenueReportRow row = new("2026-01", 10000m, 9000m, 500m, "USD", 100, 90);

        row.Period.Should().Be("2026-01");
        row.GrossRevenue.Should().Be(10000m);
        row.NetRevenue.Should().Be(9000m);
        row.Refunds.Should().Be(500m);
        row.Currency.Should().Be("USD");
        row.InvoiceCount.Should().Be(100);
        row.PaymentCount.Should().Be(90);
    }

    [Fact]
    public void PaymentReportRow_Constructor_SetsAllProperties()
    {
        Guid paymentId = Guid.NewGuid();
        DateTime paymentDate = DateTime.UtcNow;

        PaymentReportRow row = new(paymentId, "INV-001", 250m, "USD", "card", "Completed", paymentDate);

        row.PaymentId.Should().Be(paymentId);
        row.InvoiceNumber.Should().Be("INV-001");
        row.Amount.Should().Be(250m);
        row.Method.Should().Be("card");
        row.Status.Should().Be("Completed");
        row.PaymentDate.Should().Be(paymentDate);
    }

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
            ResetToken = "token-abc-123"
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

    // ── Metering events ──────────────────────────────────────────────────

    [Fact]
    public void QuotaThresholdReachedEvent_WithAllProperties_HasCorrectValues()
    {
        QuotaThresholdReachedEvent evt = new()
        {
            TenantId = Guid.NewGuid(),
            MeterCode = "api-calls",
            MeterDisplayName = "API Calls",
            CurrentUsage = 8000m,
            Limit = 10000m,
            PercentUsed = 80,
            Period = "2026-01"
        };

        evt.MeterCode.Should().Be("api-calls");
        evt.PercentUsed.Should().Be(80);
        evt.CurrentUsage.Should().Be(8000m);
    }

    [Fact]
    public void UsageFlushedEvent_WithAllProperties_HasCorrectValues()
    {
        DateTime flushedAt = DateTime.UtcNow;

        UsageFlushedEvent evt = new()
        {
            FlushedAt = flushedAt,
            RecordCount = 500
        };

        evt.FlushedAt.Should().Be(flushedAt);
        evt.RecordCount.Should().Be(500);
    }

    [Fact]
    public void QuotaStatus_Constructor_SetsAllProperties()
    {
        QuotaStatus status = new("api-calls", 8000, 10000, 80m, false);

        status.MeterCode.Should().Be("api-calls");
        status.Used.Should().Be(8000);
        status.Limit.Should().Be(10000);
        status.PercentUsed.Should().Be(80m);
        status.IsExceeded.Should().BeFalse();
    }

    [Fact]
    public void QuotaStatus_WhenExceeded_IsExceededIsTrue()
    {
        QuotaStatus status = new("api-calls", 10001, 10000, 100.01m, true);

        status.IsExceeded.Should().BeTrue();
    }

    // ── Messaging events ─────────────────────────────────────────────────

    [Fact]
    public void ConversationCreatedIntegrationEvent_WithAllProperties_HasCorrectValues()
    {
        Guid conversationId = Guid.NewGuid();
        List<Guid> participants = [Guid.NewGuid(), Guid.NewGuid()];

        ConversationCreatedIntegrationEvent evt = new()
        {
            ConversationId = conversationId,
            ParticipantIds = participants,
            CreatedAt = DateTimeOffset.UtcNow,
            TenantId = Guid.NewGuid()
        };

        evt.ConversationId.Should().Be(conversationId);
        evt.ParticipantIds.Should().HaveCount(2);
    }

    [Fact]
    public void MessageSentIntegrationEvent_WithAllProperties_HasCorrectValues()
    {
        Guid messageId = Guid.NewGuid();
        Guid senderId = Guid.NewGuid();

        MessageSentIntegrationEvent evt = new()
        {
            ConversationId = Guid.NewGuid(),
            MessageId = messageId,
            SenderId = senderId,
            Content = "Hello!",
            SentAt = DateTimeOffset.UtcNow,
            TenantId = Guid.NewGuid(),
            ParticipantIds = [senderId]
        };

        evt.MessageId.Should().Be(messageId);
        evt.SenderId.Should().Be(senderId);
        evt.Content.Should().Be("Hello!");
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
            AdminEmail = "admin@foundry.dev"
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
            AdminEmail = "admin@foundry.dev"
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

    // ── Metering usage rows ──────────────────────────────────────────────

    [Fact]
    public void UsageReportRow_Constructor_SetsAllProperties()
    {
        DateTime date = DateTime.UtcNow.Date;

        UsageReportRow row = new(date, "api-calls", 1000L, "requests", 5.00m, "USD");

        row.Date.Should().Be(date);
        row.Metric.Should().Be("api-calls");
        row.Quantity.Should().Be(1000L);
        row.Unit.Should().Be("requests");
        row.BillableAmount.Should().Be(5.00m);
        row.Currency.Should().Be("USD");
    }
}
