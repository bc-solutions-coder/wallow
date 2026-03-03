using Foundry.Identity.Application.Commands.RevokeServiceAccount;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Identity.Tests.Application.Commands;

public class RevokeServiceAccountCommandTests
{
    private readonly IServiceAccountService _serviceAccountService = Substitute.For<IServiceAccountService>();

    [Fact]
    public async Task Handle_WithValidCommand_CallsServiceWithCorrectId()
    {
        // Arrange
        ServiceAccountMetadataId accountId = ServiceAccountMetadataId.New();
        RevokeServiceAccountCommand command = new(accountId);

        RevokeServiceAccountHandler handler = new(_serviceAccountService);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _serviceAccountService.Received(1).RevokeAsync(
            accountId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsSuccessResult()
    {
        // Arrange
        RevokeServiceAccountCommand command = new(ServiceAccountMetadataId.New());
        RevokeServiceAccountHandler handler = new(_serviceAccountService);

        // Act
        Result result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        // Arrange
        RevokeServiceAccountCommand command = new(ServiceAccountMetadataId.New());
        RevokeServiceAccountHandler handler = new(_serviceAccountService);
        using CancellationTokenSource cts = new();

        // Act
        await handler.Handle(command, cts.Token);

        // Assert
        await _serviceAccountService.Received(1).RevokeAsync(
            Arg.Any<ServiceAccountMetadataId>(),
            cts.Token);
    }
}
