using Amazon.S3;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Foundry.Api.HealthChecks;

internal sealed class S3HealthCheck(IAmazonS3 s3Client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await s3Client.ListBucketsAsync(cancellationToken);
            return HealthCheckResult.Healthy("S3 is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("S3 is unreachable.", ex);
        }
    }
}
