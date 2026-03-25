using Wallow.Identity.Infrastructure.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Identity.Tests.Infrastructure;

public class InfrastructureMiscTests
{
    #region ServiceAccountTrackingMiddlewareExtensions

    [Fact]
    public void UseServiceAccountTracking_DoesNotThrow()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        ServiceProvider provider = services.BuildServiceProvider();

        IApplicationBuilder builder = new ApplicationBuilder(provider);

        Action act = () => builder.UseServiceAccountTracking();

        act.Should().NotThrow();
    }

    #endregion

}
