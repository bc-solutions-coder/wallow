using Microsoft.Extensions.DependencyInjection;
using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Domain.Entities;
using Wallow.Inquiries.Domain.Identity;
using Wallow.Notifications.Application.Channels.InApp.Interfaces;
using Wallow.Notifications.Domain.Channels.InApp.Entities;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Contracts.Inquiries.Events;
using Wallow.Shared.Kernel.Pagination;
using Wallow.Tests.Common.Factories;
using Wallow.Tests.Common.Helpers;
using Wolverine.Tracking;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Checks that retrying a poisoned email handler leaves sibling handlers with one execution each.
/// Uses tracked envelope destinations plus inquiry/notification state where available.
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class MultipleHandlerSeparationTests(WallowApiFactory factory)
{
    /// <summary>
    /// Invalid recipient used to fail SendEmail validation.
    /// </summary>
    private const string PoisonedRecipient = "separation-probe-not-an-email";

    private static readonly TimeSpan _trackingTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task EmailVerified_LinksTheInquiry_WhenTheNotificationsSiblingFails()
    {
        // The inquiry accepts this address, while email validation rejects it.
        Guid userId = Guid.NewGuid();
        string email = $"{PoisonedRecipient}-{Guid.NewGuid():N}";

        InquiryId inquiryId = await SeedUnlinkedInquiryAsync(email);

        ITrackedSession session = await PublishAndWaitAsync(new EmailVerifiedEvent
        {
            UserId = userId,
            TenantId = TestConstants.TestTenantId,
            Email = email,
            FirstName = "Separation",
            LastName = "Probe"
        });

        AssertHandlersRanIndependently<EmailVerifiedEvent>(session, handlerCount: 2);

        using IServiceScope scope = factory.Services.CreateScope();
        IInquiryRepository repository = scope.ServiceProvider.GetRequiredService<IInquiryRepository>();
        Inquiry? linked = await repository.GetByIdAsync(inquiryId);

        linked.Should().NotBeNull();
        linked!.SubmitterId.Should().Be(
            userId.ToString(),
            "the Inquiries link-up committed before the Notifications welcome email failed, and a " +
            "failure in another module must not roll it back or replay it");
    }

    [Fact]
    public async Task InquirySubmitted_WritesOneAdminNotification_WhenTheEmailSiblingFails()
    {
        // A duplicated notification row would reveal a replay of the healthy sibling.
        Guid adminUserId = Guid.NewGuid();

        ITrackedSession session = await PublishAndWaitAsync(new InquirySubmittedEvent
        {
            InquiryId = Guid.NewGuid(),
            Name = "Separation Probe",
            Email = "probe@example.com",
            Phone = "555-0100",
            ProjectType = "Separation Probe",
            Message = "Probing multiple handler separation.",
            SubmittedAt = DateTime.UtcNow,
            AdminEmail = PoisonedRecipient,
            AdminUserIds = [adminUserId]
        });

        AssertHandlersRanIndependently<InquirySubmittedEvent>(session, handlerCount: 3);
        await AssertNotificationCountAsync(adminUserId, expected: 1);
    }

    [Fact]
    public async Task InquiryCommentAdded_WritesOneSubmitterNotification_WhenTheEmailSiblingFails()
    {
        // A public comment by another author reaches the submitter notification paths.
        Guid submitterUserId = Guid.NewGuid();

        ITrackedSession session = await PublishAndWaitAsync(new InquiryCommentAddedEvent
        {
            InquiryCommentId = Guid.NewGuid(),
            InquiryId = Guid.NewGuid(),
            TenantId = TestConstants.TestTenantId,
            AuthorId = Guid.NewGuid().ToString(),
            AuthorName = "Staff Author",
            IsInternal = false,
            SubmitterEmail = PoisonedRecipient,
            SubmitterName = "Separation Probe",
            SubmitterUserId = submitterUserId,
            InquirySubject = "Separation Probe",
            CommentContent = "Probing multiple handler separation."
        });

        AssertHandlersRanIndependently<InquiryCommentAddedEvent>(session, handlerCount: 3);
        await AssertNotificationCountAsync(submitterUserId, expected: 1);
    }

    [Fact]
    public async Task InquiryStatusChanged_StillPushesOverSse_WhenTheEmailSiblingFails()
    {
        // This case checks the SSE handler through tracking, without asserting a delivered client event.
        ITrackedSession session = await PublishAndWaitAsync(new InquiryStatusChangedEvent
        {
            InquiryId = Guid.NewGuid(),
            OldStatus = "New",
            NewStatus = "Reviewed",
            ChangedAt = DateTime.UtcNow,
            SubmitterEmail = PoisonedRecipient
        });

        AssertHandlersRanIndependently<InquiryStatusChangedEvent>(session, handlerCount: 2);
    }

    private Task<ITrackedSession> PublishAndWaitAsync(object message) =>
        factory.Services.TrackActivity()

            // The poisoned handler is supposed to throw; tracking would otherwise rethrow it for us.
            .DoNotAssertOnExceptionsDetected()
            .Timeout(_trackingTimeout)
            .PublishMessageAndWaitAsync(message, null);

    /// <summary>
    /// Checks one dead-lettered destination, distinct successful destinations and execution counts.
    /// </summary>
    private static void AssertHandlersRanIndependently<TMessage>(ITrackedSession session, int handlerCount)
    {
        // Inspect the terminal dead-letter outcome.
        EnvelopeRecord[] deadLettered = RecordsOf<TMessage>(session.MovedToErrorQueue);

        deadLettered.Should().HaveCount(
            1,
            "the poisoned recipient must break exactly one handler — no failure at all and this " +
            "test proves nothing about the siblings, more than one and it is not measuring " +
            "isolation any more");

        Uri poisonedQueue = QueueOf(deadLettered[0]);

        EnvelopeRecord[] succeeded = RecordsOf<TMessage>(session.MessageSucceeded);

        succeeded.Should().HaveCount(
            handlerCount - 1,
            "every other handler for this message must succeed exactly once: under the old " +
            "ClassicCombineIntoOneLogicalHandler they shared the failing handler's single envelope, " +
            "so a sibling ordered before the failure ran twice and one ordered after it never ran " +
            "at all");

        Uri[] succeededQueues = [.. succeeded.Select(QueueOf)];

        succeededQueues.Should().OnlyHaveUniqueItems(
            "Separated gives each handler its own local:// queue, so no two successes may share one");

        succeededQueues.Should().NotContain(
            poisonedQueue,
            "a handler sharing the poisoned handler's queue shares its retry loop, which is the " +
            "duplicated side effect this setting exists to prevent");

        // Count executions to distinguish handler retries from transport deliveries.
        ILookup<bool, EnvelopeRecord> executions =
            RecordsOf<TMessage>(session.Executed).ToLookup(record => QueueOf(record) == poisonedQueue);

        executions[true].Should().HaveCountGreaterThan(
            1,
            "the poisoned handler is the one that must be retried");

        executions[false].Should().HaveCount(
            handlerCount - 1,
            "each healthy handler must execute the message exactly once — the poisoned " +
            "handler's retry must not put it back in front of any of them");
    }

    /// <summary>
    /// Returns the tracked envelope destination or fails if it is missing.
    /// </summary>
    private static Uri QueueOf(EnvelopeRecord record) =>
        record.Envelope?.Destination
        ?? throw new InvalidOperationException(
            "A tracked envelope record for a published message must carry a routed envelope.");

    private static EnvelopeRecord[] RecordsOf<TMessage>(RecordCollection collection) =>
        [.. collection.RecordsInOrder().Where(record => record.Message is TMessage)];

    private async Task<InquiryId> SeedUnlinkedInquiryAsync(string email)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IInquiryRepository repository = scope.ServiceProvider.GetRequiredService<IInquiryRepository>();
        TimeProvider timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        Inquiry inquiry = Inquiry.Create(
            name: "Separation Probe",
            email: email,
            phone: "555-0100",
            company: null,
            submitterId: null,
            projectType: "Separation Probe",
            budgetRange: "Unknown",
            timeline: "Unknown",
            message: "Probing multiple handler separation.",
            submitterIpAddress: "127.0.0.1",
            timeProvider: timeProvider);

        await repository.AddAsync(inquiry);
        await repository.SaveChangesAsync();

        return inquiry.Id;
    }

    private async Task AssertNotificationCountAsync(Guid userId, int expected)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        INotificationRepository repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        PagedResult<Notification> notifications = await repository.GetByUserIdPagedAsync(
            userId, page: 1, pageSize: 50);

        notifications.Items.Should().HaveCount(
            expected,
            "the in-app handler owns its own retry loop now, so the email handler failing and " +
            "retrying beside it must not write the notification a second time");
    }
}
