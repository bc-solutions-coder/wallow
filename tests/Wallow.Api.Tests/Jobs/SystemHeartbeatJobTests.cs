using Wallow.Api.Jobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Wallow.Api.Tests.Jobs;

public class SystemHeartbeatJobTests
{
    private readonly SystemHeartbeatJob _sut = new(NullLogger<SystemHeartbeatJob>.Instance);

    [Fact]
    public async Task ExecuteAsync_WhenCalled_CompletesSuccessfully()
    {
        Task result = _sut.ExecuteAsync();

        await result;

        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCalledMultipleTimes_CompletesEachTime()
    {
        await _sut.ExecuteAsync();
        await _sut.ExecuteAsync();
        await _sut.ExecuteAsync();

        // No exceptions thrown across multiple invocations
    }
}
