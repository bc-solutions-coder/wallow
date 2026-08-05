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
/// Pins <c>MultipleHandlerBehavior.Separated</c>: when a message type has several handlers, one
/// handler failing must not re-run the siblings that already committed.
/// <para>
/// Under Wolverine's default (<c>ClassicCombineIntoOneLogicalHandler</c>) every handler for a
/// message is welded into ONE logical handler behind ONE retry loop and ONE envelope. A failure in
/// a late handler therefore replays the earlier ones — a second welcome email, a second in-app
/// notification row, a second SSE push. Worse, the failure and the replay can be in different
/// modules: a Notifications email failure used to retry the Inquiries submitter link-up, a module
/// boundary crossed by a retry policy rather than by a contract.
/// </para>
/// <para>
/// The lever is data, not a test double. <c>SendEmailValidator</c> rejects a <c>To</c> that is not
/// an email address, and FluentValidation runs as Wolverine middleware, so an event carrying a
/// malformed recipient makes exactly the email handler throw inside its <c>bus.InvokeAsync</c>
/// while its siblings are untouched. Nothing in the host is stubbed for these tests.
/// </para>
/// <para>
/// The mode-discriminating assertion is the envelope ledger. Separated gives every handler its own
/// <c>local://</c> queue, so one publish becomes N envelopes on N distinct destinations, N-1 of
/// which succeed exactly once. Classic gives one envelope on one destination that fails outright,
/// so the healthy siblings show zero successes. "Exactly once" is what rules out the replay: with
/// <c>OnAnyException().RetryTimes(1)</c> a Classic-mode sibling ordered before the failure runs
/// twice and one ordered after it runs not at all — never exactly once.
/// </para>
/// <para>
/// Three of the four message types also get a committed-state assertion (the linked inquiry, the
/// in-app notification rows). <see cref="InquiryStatusChangedEvent"/>'s only healthy sibling is the
/// SSE push, whose sole effect is a Redis publish that leaves nothing to read back, so that one
/// rests on the envelope ledger alone.
/// </para>
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class MultipleHandlerSeparationTests(WallowApiFactory factory)
{
    /// <summary>
    /// Not an email address, so <c>SendEmailValidator</c> fails it before the handler body runs.
    /// Whichever handler carries it into <c>SendEmailCommand</c> is the one that throws.
    /// </summary>
    private const string PoisonedRecipient = "separation-probe-not-an-email";

    private static readonly TimeSpan _trackingTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task EmailVerified_LinksTheInquiry_WhenTheNotificationsSiblingFails()
    {
        // The lead case: the two handlers live in different modules. Inquiries links every unlinked
        // inquiry left behind by the address that was just verified; Notifications sends the welcome
        // email. Reusing the poisoned string as the verified address is what puts them in conflict —
        // it is a legitimate inquiry email as far as Inquiries is concerned and a validation failure
        // as far as the email pipeline is concerned.
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
        // Three handlers: admin email, admin in-app notification, tenant SSE push. The admin email
        // address is the poisoned one, so the in-app write is the sibling that must survive — and it
        // is the one whose duplication is visible, because a replay leaves a second row.
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
        // Three handlers: submitter email, submitter in-app notification, SSE push. A public comment
        // written by someone other than the submitter is the shape that exercises all three.
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
        // Two handlers: submitter email and tenant SSE push. The SSE push writes to Redis and
        // returns nothing readable, so the envelope ledger is the whole assertion here.
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
    /// Asserts the envelope ledger for one publish: <paramref name="handlerCount"/> independent
    /// envelopes on distinct local queues, exactly one of which was retried into the dead-letter
    /// queue while every other one was delivered once and succeeded once.
    /// </summary>
    private static void AssertHandlersRanIndependently<TMessage>(ITrackedSession session, int handlerCount)
    {
        // A handler that exhausts its retries lands in MovedToErrorQueue, not MessageFailed —
        // OnAnyException().RetryTimes(1).Then.MoveToErrorQueue() is the repo's terminal policy, and
        // tracking reports the terminal outcome.
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

        // The sharpest statement of the defect: the retry replayed the failing handler and nothing
        // else. Count deliveries rather than executions so the claim is about what the transport
        // handed each handler, and stay off the exact retry count so tuning the error policy does
        // not rewrite this test.
        ILookup<bool, EnvelopeRecord> deliveries =
            RecordsOf<TMessage>(session.Received).ToLookup(record => QueueOf(record) == poisonedQueue);

        deliveries[true].Should().HaveCountGreaterThan(
            1,
            "the poisoned handler is the one that must be retried");

        deliveries[false].Should().HaveCount(
            handlerCount - 1,
            "each healthy handler must be delivered the message exactly once — the poisoned " +
            "handler's retry must not put it back in front of any of them");
    }

    /// <summary>
    /// The <c>local://</c> queue an envelope was routed to. Under Separated that is the handler's
    /// own queue, which is what makes "did these two handlers share a retry loop?" answerable.
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
