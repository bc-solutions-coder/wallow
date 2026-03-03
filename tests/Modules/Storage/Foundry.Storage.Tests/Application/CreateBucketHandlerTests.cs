using Foundry.Shared.Kernel.Identity;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;
using Foundry.Storage.Application.Commands.CreateBucket;
using Foundry.Storage.Application.DTOs;
using Foundry.Storage.Application.Interfaces;
using Foundry.Storage.Domain.Entities;
using Foundry.Storage.Domain.Enums;

namespace Foundry.Storage.Tests.Application;

public class CreateBucketHandlerTests
{
    private readonly IStorageBucketRepository _bucketRepository;
    private readonly CreateBucketHandler _handler;

    public CreateBucketHandlerTests()
    {
        _bucketRepository = Substitute.For<IStorageBucketRepository>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.New());
        _handler = new CreateBucketHandler(_bucketRepository, tenantContext);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesBucket()
    {
        CreateBucketCommand command = new("test-bucket", "A test bucket");
        _bucketRepository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<BucketDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("test-bucket");
        result.Value.Description.Should().Be("A test bucket");
        _bucketRepository.Received(1).Add(Arg.Any<StorageBucket>());
        await _bucketRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBucketAlreadyExists_ReturnsConflictFailure()
    {
        CreateBucketCommand command = new("existing-bucket");
        _bucketRepository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<BucketDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Conflict");
        result.Error.Message.Should().Contain("existing-bucket");
        _bucketRepository.DidNotReceive().Add(Arg.Any<StorageBucket>());
    }

    [Fact]
    public async Task Handle_WithRetentionPolicy_CreatesBucketWithRetention()
    {
        CreateBucketCommand command = new(
            "archive-bucket",
            RetentionDays: 30,
            RetentionAction: RetentionAction.Archive);
        _bucketRepository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<BucketDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Retention.Should().NotBeNull();
        result.Value.Retention!.Days.Should().Be(30);
        result.Value.Retention.Action.Should().Be("Archive");
    }

    [Fact]
    public async Task Handle_WithoutRetentionPolicy_CreatesBucketWithNoRetention()
    {
        CreateBucketCommand command = new("simple-bucket");
        _bucketRepository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<BucketDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Retention.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithAllOptions_CreatesBucketWithCorrectSettings()
    {
        CreateBucketCommand command = new(
            "full-bucket",
            Description: "Full options",
            Access: AccessLevel.Public,
            MaxFileSizeBytes: 1024 * 1024,
            AllowedContentTypes: new List<string> { "image/png", "image/jpeg" },
            RetentionDays: 90,
            RetentionAction: RetentionAction.Delete,
            Versioning: true);
        _bucketRepository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<BucketDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("full-bucket");
        result.Value.Description.Should().Be("Full options");
        result.Value.Access.Should().Be("Public");
        result.Value.MaxFileSizeBytes.Should().Be(1024 * 1024);
        result.Value.AllowedContentTypes.Should().Contain("image/png");
        result.Value.AllowedContentTypes.Should().Contain("image/jpeg");
        result.Value.Versioning.Should().BeTrue();
        result.Value.Retention!.Days.Should().Be(90);
    }

    [Fact]
    public async Task Handle_WithOnlyRetentionDays_NoRetentionActionSet_DoesNotSetRetention()
    {
        CreateBucketCommand command = new("partial-retention", RetentionDays: 30);
        _bucketRepository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<BucketDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Retention.Should().BeNull();
    }
}
