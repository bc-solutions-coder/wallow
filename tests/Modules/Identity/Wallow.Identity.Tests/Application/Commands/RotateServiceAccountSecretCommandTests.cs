using Wallow.Identity.Application.Commands.RotateServiceAccountSecret;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Identity;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Tests.Application.Commands;

public class RotateServiceAccountSecretCommandTests
{
    private readonly IServiceAccountService _serviceAccountService = Substitute.For<IServiceAccountService>();

    [Fact]
    public async Task Handle_WithValidCommand_CallsServiceWithCorrectId()
    {
        // Arrange
        ServiceAccountMetadataId accountId = ServiceAccountMetadataId.New();
        RotateServiceAccountSecretCommand command = new(accountId);

        SecretRotatedResult expectedResult = new(
            "new-secret-123",
            DateTime.UtcNow);

        _serviceAccountService
            .RotateSecretAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        RotateServiceAccountSecretHandler handler = new(_serviceAccountService);

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
        RotateServiceAccountSecretCommand command = new(ServiceAccountMetadataId.New());
        string newSecret = "rotated-secret-xyz";
        DateTime rotatedAt = DateTime.UtcNow;

        _serviceAccountService
            .RotateSecretAsync(Arg.Any<ServiceAccountMetadataId>(), Arg.Any<CancellationToken>())
            .Returns(new SecretRotatedResult(newSecret, rotatedAt));

        RotateServiceAccountSecretHandler handler = new(_serviceAccountService);

        // Act
        Result<SecretRotatedResult> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Value.NewClientSecret.Should().Be(newSecret);
        result.Value.RotatedAt.Should().BeCloseTo(rotatedAt, TimeSpan.FromSeconds(1));
    }
}
