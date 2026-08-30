using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

/// <summary>
/// The email an organization's admins get when the platform suspends their organization: one send
/// per recipient carrying the operator's reason, and nothing at all when Identity resolved nobody
/// to tell. Lifting the suspension sends no email — the suspension notice is the only one.
/// </summary>
public class OrganizationPlatformSuspendedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IEmailTemplateService _templateService = Substitute.For<IEmailTemplateService>();

    public OrganizationPlatformSuspendedNotificationHandlerTests() =>
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
    public async Task Handle_RendersTheSuspensionTemplateWithTheOperatorsReason()
    {
        object? model = null;
        _templateService
            .RenderAsync(Arg.Any<string>(), Arg.Do<object>(m => model = m), Arg.Any<CancellationToken>())
            .Returns("<html>rendered</html>");

        await Handle(Event(["owner@test.com"]));

        await _templateService.Received(1).RenderAsync(
            "organizationplatformsuspended", Arg.Any<object>(), Arg.Any<CancellationToken>());
        string? reason = model?.GetType().GetProperty("Reason")?.GetValue(model) as string;
        reason.Should().Be("Fraud investigation");
    }

    [Fact]
    public async Task Handle_ComposesTheOrganizationUrlFromConfiguredWebUrl()
    {
        Guid tenantId = Guid.NewGuid();
        object? model = null;
        _templateService
            .RenderAsync(Arg.Any<string>(), Arg.Do<object>(m => model = m), Arg.Any<CancellationToken>())
            .Returns("<html>rendered</html>");

        await Handle(
            Event(["owner@test.com"]) with { TenantId = tenantId, OrganizationId = tenantId },
            new Dictionary<string, string?> { ["ServiceUrls:WebUrl"] = "https://app.example.com/" });

        string? organizationUrl = model?.GetType().GetProperty("OrganizationUrl")?.GetValue(model) as string;
        organizationUrl.Should().Be($"https://app.example.com/dashboard/organizations/{tenantId}");
    }

    private static OrganizationSuspendedByPlatformEvent Event(IReadOnlyList<string> recipients) => new()
    {
        OrganizationId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        OrganizationName = "Contoso",
        ActorId = Guid.NewGuid(),
        Reason = "Fraud investigation",
        RecipientEmails = recipients
    };

    private Task Handle(OrganizationSuspendedByPlatformEvent @event, Dictionary<string, string?>? settings = null) =>
        OrganizationPlatformSuspendedNotificationHandler.Handle(
            @event,
            _templateService,
            _bus,
            new ConfigurationBuilder()
                .AddInMemoryCollection(settings ?? [])
                .Build());
}
