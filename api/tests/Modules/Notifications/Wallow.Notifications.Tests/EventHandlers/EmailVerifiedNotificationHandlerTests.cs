using Microsoft.Extensions.Configuration;
using Wallow.Notifications.Application.Channels.Email.Commands.SendEmail;
using Wallow.Notifications.Application.Channels.Email.Interfaces;
using Wallow.Notifications.Application.EventHandlers;
using Wallow.Shared.Contracts.Identity.Events;
using Wolverine;

namespace Wallow.Notifications.Tests.EventHandlers;

public class EmailVerifiedNotificationHandlerTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly IEmailTemplateService _templateService = Substitute.For<IEmailTemplateService>();
    private readonly IConfiguration _configuration = Substitute.For<IConfiguration>();

    [Fact]
    public async Task Handle_RendersWelcomeTemplateAndSendsEmail()
    {
        EmailVerifiedEvent @event = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "John",
            LastName = "Doe"
        };

        _configuration["AppUrl"].Returns("http://myapp.com");
        _configuration["Branding:AppName"].Returns("TestApp");

        string renderedHtml = "<p>Welcome John! Visit http://myapp.com</p>";
        _templateService.RenderAsync("welcomeemail", Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(renderedHtml);

        await EmailVerifiedNotificationHandler.Handle(@event, _bus, _templateService, _configuration);

        await _templateService.Received(1).RenderAsync("welcomeemail", Arg.Any<object>(), Arg.Any<CancellationToken>());

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd =>
                cmd.To == "user@test.com" &&
                cmd.Subject == "Welcome to TestApp" &&
                cmd.Body == renderedHtml),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_WhenAppUrlNotConfigured_FallsBackToAuthUrl()
    {
        EmailVerifiedEvent @event = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "Jane",
            LastName = "Smith"
        };

        _configuration["AppUrl"].Returns((string?)null);
        _configuration["AuthUrl"].Returns("http://auth.example.com");

        _templateService.RenderAsync("welcomeemail", Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("<p>Welcome</p>");

        await EmailVerifiedNotificationHandler.Handle(@event, _bus, _templateService, _configuration);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd => cmd.To == "user@test.com"),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Handle_WhenNoUrlConfigured_UsesDefaultUrl()
    {
        EmailVerifiedEvent @event = new()
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "Jane",
            LastName = "Smith"
        };

        _configuration["AppUrl"].Returns((string?)null);
        _configuration["AuthUrl"].Returns((string?)null);

        _templateService.RenderAsync("welcomeemail", Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("<p>Welcome</p>");

        await EmailVerifiedNotificationHandler.Handle(@event, _bus, _templateService, _configuration);

        await _bus.Received(1).InvokeAsync(
            Arg.Is<SendEmailCommand>(cmd => cmd.To == "user@test.com"),
            Arg.Any<CancellationToken>(),
            Arg.Any<TimeSpan?>());
    }
}
