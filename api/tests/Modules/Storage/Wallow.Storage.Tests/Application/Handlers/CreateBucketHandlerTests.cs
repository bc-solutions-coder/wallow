using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.Commands.CreateBucket;
using Wallow.Storage.Application.DTOs;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Domain.Entities;
using Wallow.Storage.Domain.Enums;

namespace Wallow.Storage.Tests.Application.Handlers;

public class CreateBucketHandlerTests
{
    private readonly IStorageBucketRepository _repository;
    private readonly CreateBucketHandler _handler;

    public CreateBucketHandlerTests()
    {
        _repository = Substitute.For<IStorageBucketRepository>();
        ITenantContext tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns(TenantId.New());
        _handler = new CreateBucketHandler(_repository, tenantContext);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesBucket()
    {
        CreateBucketCommand command = new(Name: "my-bucket");

        _repository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<BucketDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Name.Should().Be("my-bucket");

        _repository.Received(1).Add(Arg.Any<StorageBucket>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithDuplicateName_ReturnsFailure()
    {
        CreateBucketCommand command = new(Name: "existing-bucket");

        _repository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(true);

        Result<BucketDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Conflict");

        _repository.DidNotReceive().Add(Arg.Any<StorageBucket>());
    }

    [Fact]
    public async Task Handle_WithAllOptions_CreatesBucketWithOptions()
    {
        CreateBucketCommand command = new(
            Name: "full-bucket",
            Description: "A fully configured bucket",
            Access: AccessLevel.Public,
            MaxFileSizeBytes: 10_485_760,
            AllowedContentTypes: ["image/png", "image/jpeg"],
            RetentionDays: 30,
            RetentionAction: RetentionAction.Archive,
            Versioning: true);

        _repository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<BucketDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("full-bucket");
        result.Value.Description.Should().Be("A fully configured bucket");
        result.Value.Access.Should().Be("Public");
        result.Value.MaxFileSizeBytes.Should().Be(10_485_760);
        result.Value.Versioning.Should().BeTrue();
        result.Value.Retention.Should().NotBeNull();
        result.Value.Retention!.Days.Should().Be(30);
    }

    [Fact]
    public async Task Handle_WhenExecutedTwiceWithSameName_SecondExecutionFails()
    {
        CreateBucketCommand command = new(Name: "duplicate-bucket");

        _repository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(false, true);

        Result<BucketDto> result1 = await _handler.Handle(command, CancellationToken.None);
        Result<BucketDto> result2 = await _handler.Handle(command, CancellationToken.None);

        result1.IsSuccess.Should().BeTrue();
        result2.IsFailure.Should().BeTrue();
        result2.Error.Code.Should().Contain("Conflict");
        _repository.Received(1).Add(Arg.Any<StorageBucket>());
    }

    [Fact]
    public async Task Handle_WithoutRetention_CreatesBucketWithNoRetention()
    {
        CreateBucketCommand command = new(
            Name: "no-retention",
            RetentionDays: null,
            RetentionAction: null);

        _repository.ExistsByNameAsync(command.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        Result<BucketDto> result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Retention.Should().BeNull();
    }
}
