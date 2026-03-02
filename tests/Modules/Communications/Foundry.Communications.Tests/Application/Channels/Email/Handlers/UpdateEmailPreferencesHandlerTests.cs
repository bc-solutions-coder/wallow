using Foundry.Communications.Application.Channels.Email.Commands.UpdateEmailPreferences;
using Foundry.Communications.Application.Channels.Email.DTOs;
using Foundry.Communications.Application.Channels.Email.Interfaces;
using Foundry.Communications.Domain.Channels.Email.Entities;
using Foundry.Communications.Domain.Channels.Email.Enums;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Communications.Tests.Application.Channels.Email.Handlers;

public class UpdateEmailPreferencesHandlerTests
{
    private readonly IEmailPreferenceRepository _repository;
    private readonly UpdateEmailPreferencesHandler _handler;

    public UpdateEmailPreferencesHandlerTests()
    {
        _repository = Substitute.For<IEmailPreferenceRepository>();
        _handler = new UpdateEmailPreferencesHandler(_repository, TimeProvider.System);
    }

    [Fact]
    public async Task Handle_WhenPreferenceDoesNotExist_CreatesNewPreference()
    {
        Guid userId = Guid.NewGuid();
        _repository.GetByUserAndTypeAsync(userId, NotificationType.SystemNotification, Arg.Any<CancellationToken>())
            .Returns((EmailPreference?)null);

        UpdateEmailPreferencesCommand command = new(userId, NotificationType.SystemNotification, true);

        Result<EmailPreferenceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.NotificationType.Should().Be(NotificationType.SystemNotification);
        result.Value.IsEnabled.Should().BeTrue();
        _repository.Received(1).Add(Arg.Any<EmailPreference>());
    }

    [Fact]
    public async Task Handle_WhenPreferenceExists_UpdatesToEnabled()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference preference = EmailPreference.Create(userId, NotificationType.SystemNotification, false);

        _repository.GetByUserAndTypeAsync(userId, NotificationType.SystemNotification, Arg.Any<CancellationToken>())
            .Returns(preference);

        UpdateEmailPreferencesCommand command = new(userId, NotificationType.SystemNotification, true);

        Result<EmailPreferenceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeTrue();
        _repository.DidNotReceive().Add(Arg.Any<EmailPreference>());
    }

    [Fact]
    public async Task Handle_WhenPreferenceExists_UpdatesToDisabled()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference preference = EmailPreference.Create(userId, NotificationType.SystemNotification, true);

        _repository.GetByUserAndTypeAsync(userId, NotificationType.SystemNotification, Arg.Any<CancellationToken>())
            .Returns(preference);

        UpdateEmailPreferencesCommand command = new(userId, NotificationType.SystemNotification, false);

        Result<EmailPreferenceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SavesChanges()
    {
        Guid userId = Guid.NewGuid();
        _repository.GetByUserAndTypeAsync(userId, NotificationType.TaskAssigned, Arg.Any<CancellationToken>())
            .Returns((EmailPreference?)null);

        UpdateEmailPreferencesCommand command = new(userId, NotificationType.TaskAssigned, true);

        await _handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        using CancellationTokenSource cts = new();
        Guid userId = Guid.NewGuid();
        _repository.GetByUserAndTypeAsync(userId, NotificationType.TaskAssigned, Arg.Any<CancellationToken>())
            .Returns((EmailPreference?)null);

        UpdateEmailPreferencesCommand command = new(userId, NotificationType.TaskAssigned, true);

        await _handler.Handle(command, cts.Token);

        await _repository.Received(1).GetByUserAndTypeAsync(userId, NotificationType.TaskAssigned, cts.Token);
        await _repository.Received(1).SaveChangesAsync(cts.Token);
    }
}
