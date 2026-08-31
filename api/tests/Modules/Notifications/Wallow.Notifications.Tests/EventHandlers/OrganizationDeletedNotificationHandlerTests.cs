using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

/// <summary>
/// The email an organization's admins get when the organization is permanently deleted: one
/// send per recipient, addressed from the emails the event carried — the memberships no longer
/// exist to resolve them from — and pointing at the organizations list, since the deleted
/// organization's own page is gone. No recipients means no email and no error.
/// </summary>
public class OrganizationDeletedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IEmailTemplateService _templateService = Substitute.For<IEmailTemplateService>();

    public OrganizationDeletedNotificationHandlerTests() =>
        _templateService
            .RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("<html>rendered</html>");

    [Fact]
    public async Task Handle_SendsOneEmailPerRecipient()
    {
        await Handle(Event(["owner@test.com", "ops@test.com"]));

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
    public async Task Handle_RendersTheDeletionTemplateWithTheOrganizationsName()
    {
        object? model = null;
        _templateService
            .RenderAsync(Arg.Any<string>(), Arg.Do<object>(m => model = m), Arg.Any<CancellationToken>())
            .Returns("<html>rendered</html>");

        await Handle(Event(["owner@test.com"]));

        await _templateService.Received(1).RenderAsync(
            "organizationdeleted", Arg.Any<object>(), Arg.Any<CancellationToken>());
        string? name = model?.GetType().GetProperty("OrganizationName")?.GetValue(model) as string;
        name.Should().Be("Contoso");
    }

    [Fact]
    public async Task Handle_PointsAtTheOrganizationsListFromConfiguredWebUrl()
    {
        object? model = null;
        _templateService
            .RenderAsync(Arg.Any<string>(), Arg.Do<object>(m => model = m), Arg.Any<CancellationToken>())
            .Returns("<html>rendered</html>");

        await Handle(
            Event(["owner@test.com"]),
            new Dictionary<string, string?> { ["ServiceUrls:WebUrl"] = "https://app.example.com/" });

        string? dashboardUrl = model?.GetType().GetProperty("DashboardUrl")?.GetValue(model) as string;
        dashboardUrl.Should().Be("https://app.example.com/dashboard/organizations");
    }

    private static OrganizationDeletedEvent Event(IReadOnlyList<string> recipients) => new()
    {
        OrganizationId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        OrganizationName = "Contoso",
        ActorId = Guid.NewGuid(),
        RecipientEmails = recipients
    };

    private Task Handle(OrganizationDeletedEvent @event, Dictionary<string, string?>? settings = null) =>
        OrganizationDeletedNotificationHandler.Handle(
            @event,
            _templateService,
            _bus,
            new ConfigurationBuilder()
                .AddInMemoryCollection(settings ?? [])
                .Build());
}
