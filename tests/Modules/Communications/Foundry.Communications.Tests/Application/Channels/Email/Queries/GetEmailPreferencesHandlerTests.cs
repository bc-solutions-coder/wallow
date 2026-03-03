using Foundry.Communications.Application.Channels.Email.DTOs;
using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Application.Channels.Email.Queries.GetEmailPreferences;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Enums;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Channels.Email.Queries;

public class GetEmailPreferencesHandlerTests
{
    private readonly IEmailPreferenceRepository _repository;
    private readonly GetEmailPreferencesHandler _handler;

    public GetEmailPreferencesHandlerTests()
    {
        _repository = Substitute.For<IEmailPreferenceRepository>();
        _handler = new GetEmailPreferencesHandler(_repository);
    }

    [Fact]
    public async Task Handle_ReturnsPreferencesForUser()
    {
        Guid userId = Guid.NewGuid();
        List<EmailPreference> preferences = new()
        {
            EmailPreference.Create(userId, NotificationType.SystemNotification, true),
            EmailPreference.Create(userId, NotificationType.TaskAssigned, false)
        };

        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(preferences);

        GetEmailPreferencesQuery query = new(userId);

        Result<IReadOnlyList<EmailPreferenceDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WhenNoPreferences_ReturnsEmptyList()
    {
        Guid userId = Guid.NewGuid();
        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<EmailPreference>());

        GetEmailPreferencesQuery query = new(userId);

        Result<IReadOnlyList<EmailPreferenceDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsPreferenceFieldsCorrectly()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference preference = EmailPreference.Create(userId, NotificationType.BillingInvoice, true);

        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<EmailPreference> { preference });

        GetEmailPreferencesQuery query = new(userId);

        Result<IReadOnlyList<EmailPreferenceDto>> result = await _handler.Handle(query, CancellationToken.None);

        EmailPreferenceDto dto = result.Value[0];
        dto.UserId.Should().Be(userId);
        dto.NotificationType.Should().Be(NotificationType.BillingInvoice);
        dto.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        Guid userId = Guid.NewGuid();
        _repository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<EmailPreference>());

        GetEmailPreferencesQuery query = new(userId);

        await _handler.Handle(query, cts.Token);

        await _repository.Received(1).GetByUserIdAsync(userId, cts.Token);
    }
}
