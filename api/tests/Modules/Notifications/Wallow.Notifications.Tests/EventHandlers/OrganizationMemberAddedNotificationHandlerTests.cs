using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class OrganizationMemberAddedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IEmailTemplateService _templateService = Substitute.For<IEmailTemplateService>();
    private readonly IConfiguration _configuration;

    public OrganizationMemberAddedNotificationHandlerTests()
    {
        _templateService.RenderAsync(Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("<html>rendered</html>"));

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppUrl"] = "https://app.test.com"
            })
            .Build();
    }

    [Fact]
    public async Task Handle_SendsWelcomeEmailToNewMember()
    {
        OrganizationMemberAddedEvent @event = new()
        {
            OrganizationId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Email = "newmember@test.com"
        };

        await OrganizationMemberAddedNotificationHandler.Handle(
            @event, _templateService, _bus, _configuration);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "newmember@test.com" &&
                cmd.Subject.Contains("Added to an Organization")),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_UsesOrganizationMemberAddedTemplate()
    {
        OrganizationMemberAddedEvent @event = new()
        {
            OrganizationId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Email = "newmember@test.com"
        };

        await OrganizationMemberAddedNotificationHandler.Handle(
            @event, _templateService, _bus, _configuration);

        await _templateService.Received(1).RenderAsync(
            "organizationmemberadded",
            Arg.Any<object>(),
            Arg.Any<CancellationToken>());
    }
}
