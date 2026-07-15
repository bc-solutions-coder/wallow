using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wallow.Api.HealthChecks;

namespace Wallow.Api.Tests.HealthChecks;

public class S3HealthCheckTests
{
    private readonly IAmazonS3 _s3Client = Substitute.For<IAmazonS3>();
    private readonly S3HealthCheck _sut;

    public S3HealthCheckTests()
    {
        _sut = new S3HealthCheck(_s3Client);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenS3Reachable_ReturnsHealthy()
    {
        _s3Client.ListBucketsAsync(Arg.Any<CancellationToken>())
            .Returns(new ListBucketsResponse());

        HealthCheckResult result = await _sut.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("S3 is reachable.");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenS3Unreachable_ReturnsUnhealthy()
    {
        AmazonS3Exception exception = new("Connection refused");
        _s3Client.ListBucketsAsync(Arg.Any<CancellationToken>())
            .Returns<ListBucketsResponse>(_ => throw exception);

        HealthCheckResult result = await _sut.CheckHealthAsync(
            new HealthCheckContext(), CancellationToken.None);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("S3 is unreachable.");
        result.Exception.Should().BeSameAs(exception);
    }
}
