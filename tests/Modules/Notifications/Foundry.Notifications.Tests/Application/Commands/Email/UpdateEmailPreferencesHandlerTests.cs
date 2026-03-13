using Foundry.Notifications.Application.Channels.Email.Commands.UpdateEmailPreferences;
using Foundry.Notifications.Application.Channels.Email.DTOs;
using Foundry.Notifications.Application.Channels.Email.Interfaces;
using Foundry.Notifications.Domain.Channels.Email.Entities;
using Foundry.Notifications.Domain.Enums;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Notifications.Tests.Application.Commands.Email;

public class UpdateEmailPreferencesHandlerTests
{
    private readonly IEmailPreferenceRepository _preferenceRepository = Substitute.For<IEmailPreferenceRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly UpdateEmailPreferencesHandler _handler;

    public UpdateEmailPreferencesHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(DateTimeOffset.UtcNow);
        _handler = new UpdateEmailPreferencesHandler(_preferenceRepository, _timeProvider);
    }

    [Fact]
    public async Task Handle_WhenPreferenceDoesNotExist_CreatesNewPreference()
    {
        Guid userId = Guid.NewGuid();
        UpdateEmailPreferencesCommand command = new(userId, NotificationType.BillingInvoice, IsEnabled: true);

        _preferenceRepository
            .GetByUserAndTypeAsync(userId, NotificationType.BillingInvoice, Arg.Any<CancellationToken>())
            .Returns((EmailPreference?)null);

        Result<EmailPreferenceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeTrue();
        _preferenceRepository.Received(1).Add(Arg.Any<EmailPreference>());
        await _preferenceRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPreferenceExists_EnablesExistingPreference()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference existing = EmailPreference.Create(userId, NotificationType.BillingInvoice, false);
        UpdateEmailPreferencesCommand command = new(userId, NotificationType.BillingInvoice, IsEnabled: true);

        _preferenceRepository
            .GetByUserAndTypeAsync(userId, NotificationType.BillingInvoice, Arg.Any<CancellationToken>())
            .Returns(existing);

        Result<EmailPreferenceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeTrue();
        _preferenceRepository.DidNotReceive().Add(Arg.Any<EmailPreference>());
    }

    [Fact]
    public async Task Handle_WhenPreferenceExists_DisablesExistingPreference()
    {
        Guid userId = Guid.NewGuid();
        EmailPreference existing = EmailPreference.Create(userId, NotificationType.TaskAssigned, true);
        UpdateEmailPreferencesCommand command = new(userId, NotificationType.TaskAssigned, IsEnabled: false);

        _preferenceRepository
            .GetByUserAndTypeAsync(userId, NotificationType.TaskAssigned, Arg.Any<CancellationToken>())
            .Returns(existing);

        Result<EmailPreferenceDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeFalse();
    }
}
