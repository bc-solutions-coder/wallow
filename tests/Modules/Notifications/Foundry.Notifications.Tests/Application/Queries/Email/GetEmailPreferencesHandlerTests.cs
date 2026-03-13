using Foundry.Notifications.Application.Channels.Email.DTOs;
using Foundry.Notifications.Application.Channels.Email.Interfaces;
using Foundry.Notifications.Application.Channels.Email.Queries.GetEmailPreferences;
using Foundry.Notifications.Domain.Channels.Email.Entities;
using Foundry.Notifications.Domain.Enums;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Tests.Application.Queries.Email;

public class GetEmailPreferencesHandlerTests
{
    private readonly IEmailPreferenceRepository _preferenceRepository = Substitute.For<IEmailPreferenceRepository>();
    private readonly GetEmailPreferencesHandler _handler;

    public GetEmailPreferencesHandlerTests()
    {
        _handler = new GetEmailPreferencesHandler(_preferenceRepository);
    }

    [Fact]
    public async Task Handle_ReturnsAllPreferencesForUser()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference pref1 = EmailPreference.Create(userId, NotificationType.BillingInvoice, true);
        EmailPreference pref2 = EmailPreference.Create(userId, NotificationType.TaskAssigned, false);

        _preferenceRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<EmailPreference> { pref1, pref2 });

        Result<IReadOnlyList<EmailPreferenceDto>> result = await _handler.Handle(
            new GetEmailPreferencesQuery(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenNoPreferences_ReturnsEmptyList()
    {
        Guid userId = Guid.NewGuid();
        _preferenceRepository
            .GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<EmailPreference>());

        Result<IReadOnlyList<EmailPreferenceDto>> result = await _handler.Handle(
            new GetEmailPreferencesQuery(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
