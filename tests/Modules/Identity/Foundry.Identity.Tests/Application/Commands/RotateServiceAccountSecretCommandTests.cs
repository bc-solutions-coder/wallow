using Foundry.Identity.Application.Commands.RotateServiceAccountSecret;
using Foundry.Identity.Application.DTOs;
using Foundry.Identity.Application.Interfaces;
using Foundry.Identity.Domain.Identity;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Identity.Tests.Application.Commands;

public class RotateServiceAccountSecretCommandTests
{
    private readonly IServiceAccountService _serviceAccountService = Substitute.For<IServiceAccountService>();

    [Fact]
    public async Task Handle_WithValidCommand_CallsServiceWithCorrectId()
    {
        // Arrange
        ServiceAccountMetadataId accountId = ServiceAccountMetadataId.New();
        RotateServiceAccountSecretCommand command = new RotateServiceAccountSecretCommand(accountId);

        SecretRotatedResult expectedResult = new SecretRotatedResult(
            "new-secret-123",
            DateTime.UtcNow);

        _serviceAccountService
            .RotateSecretAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        RotateServiceAccountSecretHandler handler = new RotateServiceAccountSecretHandler(_serviceAccountService);

        // Act
        Result<SecretRotatedResult> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedResult);

        await _serviceAccountService.Received(1).RotateSecretAsync(
            accountId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsSecretRotatedResult()
    {
        // Arrange
        RotateServiceAccountSecretCommand command = new RotateServiceAccountSecretCommand(ServiceAccountMetadataId.New());
        string newSecret = "rotated-secret-xyz";
        DateTime rotatedAt = DateTime.UtcNow;

        _serviceAccountService
            .RotateSecretAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(new SecretRotatedResult(newSecret, rotatedAt));

        RotateServiceAccountSecretHandler handler = new RotateServiceAccountSecretHandler(_serviceAccountService);

        // Act
        Result<SecretRotatedResult> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Value.NewClientSecret.Should().Be(newSecret);
        result.Value.RotatedAt.Should().BeCloseTo(rotatedAt, TimeSpan.FromSeconds(1));
    }
}
