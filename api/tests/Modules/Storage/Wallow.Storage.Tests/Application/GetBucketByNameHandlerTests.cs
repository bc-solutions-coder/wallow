using Wallow.Shared.Kernel.Identity;
using Wallow.Shared.Kernel.Results;
using Wallow.Storage.Application.DTOs;
using Wallow.Storage.Application.Interfaces;
using Wallow.Storage.Application.Queries.GetBucketByName;
using Wallow.Storage.Domain.Entities;

namespace Wallow.Storage.Tests.Application;

public class GetBucketByNameHandlerTests
{
    private readonly IStorageBucketRepository _bucketRepository;
    private readonly GetBucketByNameHandler _handler;

    public GetBucketByNameHandlerTests()
    {
        _bucketRepository = Substitute.For<IStorageBucketRepository>();
        _handler = new GetBucketByNameHandler(_bucketRepository);
    }

    [Fact]
    public async Task Handle_WhenBucketExists_ReturnsSuccessWithDto()
    {
        StorageBucket bucket = StorageBucket.Create(TenantId.New(), "my-bucket", "A description");
        GetBucketByNameQuery query = new("my-bucket");

        _bucketRepository.GetByNameAsync("my-bucket", Arg.Any<CancellationToken>())
            .Returns(bucket);

        Result<BucketDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("my-bucket");
        result.Value.Description.Should().Be("A description");
    }

    [Fact]
    public async Task Handle_WhenBucketNotFound_ReturnsNotFoundFailure()
    {
        GetBucketByNameQuery query = new("nonexistent");

        _bucketRepository.GetByNameAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((StorageBucket?)null);

        Result<BucketDto> result = await _handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("NotFound");
    }
}
