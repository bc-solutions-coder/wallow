using Foundry.Configuration.Application.FeatureFlags.Contracts;
using Foundry.Configuration.Application.FeatureFlags.DTOs;
using Foundry.Configuration.Application.FeatureFlags.Queries.GetAllFlags;
using Foundry.Configuration.Domain.Entities;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Configuration.Tests.Application.Handlers;

public class GetAllFlagsHandlerTests
{
    private readonly IFeatureFlagRepository _repository;
    private readonly GetAllFlagsHandler _handler;

    public GetAllFlagsHandlerTests()
    {
        _repository = Substitute.For<IFeatureFlagRepository>();
        _handler = new GetAllFlagsHandler(_repository);
    }

    [Fact]
    public async Task Handle_WithFlags_ReturnsSuccessWithDtos()
    {
        FeatureFlag flag1 = FeatureFlag.CreateBoolean("flag_a", "Flag A", true, TimeProvider.System);
        FeatureFlag flag2 = FeatureFlag.CreateBoolean("flag_b", "Flag B", false, TimeProvider.System);

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlag> { flag1, flag2 });

        GetAllFlagsQuery query = new();

        Result<IReadOnlyList<FeatureFlagDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Key.Should().Be("flag_a");
        result.Value[1].Key.Should().Be("flag_b");
    }

    [Fact]
    public async Task Handle_WithNoFlags_ReturnsSuccessWithEmptyList()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlag>());

        GetAllFlagsQuery query = new();

        Result<IReadOnlyList<FeatureFlagDto>> result = await _handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsDtoFieldsCorrectly()
    {
        FeatureFlag flag = FeatureFlag.CreateBoolean("dark_mode", "Dark Mode", true, TimeProvider.System, "Enable dark mode");

        _repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<FeatureFlag> { flag });

        GetAllFlagsQuery query = new();

        Result<IReadOnlyList<FeatureFlagDto>> result = await _handler.Handle(query, CancellationToken.None);

        FeatureFlagDto dto = result.Value[0];
        dto.Id.Should().Be(flag.Id.Value);
        dto.Key.Should().Be("dark_mode");
        dto.Name.Should().Be("Dark Mode");
        dto.Description.Should().Be("Enable dark mode");
        dto.DefaultEnabled.Should().BeTrue();
    }
}
