using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

/// <summary>
/// The access-request email: one send per recipient, none at all when Identity resolved nobody
/// to tell, and a review URL composed here from configuration rather than carried on the event.
/// </summary>
public class AccessRequestedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IEmailTemplateService _templateService = Substitute.For<IEmailTemplateService>();

    public AccessRequestedNotificationHandlerTests() =>
        _templateService
            .RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("<html>rendered</html>");

    [Fact]
    public async Task Handle_SendsOneEmailPerRecipient()
    {
        AccessRequestedEvent @event = Event(["owner@test.com", "ops@test.com"]);

        await Handle(@event);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd => cmd.To == "owner@test.com"),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd => cmd.To == "ops@test.com"),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_WithNoRecipients_SendsNothingAndDoesNotThrow()
    {
        await Handle(Event([]));

        await _bus.DidNotReceive().InvokeAsync(
            Arg.Any<SendEmailCommand>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_NamesTheOrganizationInTheSubject()
    {
        await Handle(Event(["owner@test.com"]));

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd => cmd.Subject.Contains("Contoso", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_RendersTheAccessRequestTemplate()
    {
        // "accessrequest", not "access-request": the template service lowercases the key but does
        // not strip hyphens, so a hyphenated name falls through to the generic default arm.
        await Handle(Event(["owner@test.com"]));

        await _templateService.Received(1).RenderAsync(
            "accessrequest", Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ComposesTheReviewUrlFromConfiguredWebUrl()
    {
        Guid tenantId = Guid.NewGuid();
        object? model = null;
        _templateService
            .RenderAsync(Arg.Any<string>(), Arg.Do<object>(m => model = m), Arg.Any<CancellationToken>())
            .Returns("<html>rendered</html>");

        await Handle(
            Event(["owner@test.com"]) with { TenantId = tenantId },
            new Dictionary<string, string?> { ["ServiceUrls:WebUrl"] = "https://app.example.com/" });

        string? reviewUrl = model?.GetType().GetProperty("ReviewUrl")?.GetValue(model) as string;
        reviewUrl.Should().Be($"https://app.example.com/dashboard/organizations/{tenantId}");
    }

    private static AccessRequestedEvent Event(IReadOnlyList<string> recipients) => new()
    {
        TenantId = Guid.NewGuid(),
        OrganizationName = "Contoso",
        RequesterUserId = Guid.NewGuid(),
        RequesterEmail = "newbie@test.com",
        RequesterName = "New Bie",
        RecipientEmails = recipients
    };

    private Task Handle(AccessRequestedEvent @event, Dictionary<string, string?>? settings = null) =>
        AccessRequestedNotificationHandler.Handle(
            @event,
            _templateService,
            _bus,
            new ConfigurationBuilder()
                .AddInMemoryCollection(settings ?? [])
                .Build());
}
