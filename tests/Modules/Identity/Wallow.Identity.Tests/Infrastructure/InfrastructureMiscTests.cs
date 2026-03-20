using Wallow.Identity.Api.Contracts.Requests;
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

    #region LogoutRequest

    [Fact]
    public void LogoutRequest_PropertyAccessible()
    {
        LogoutRequest request = new("my-refresh-token");

        request.RefreshToken.Should().Be("my-refresh-token");
    }

    [Fact]
    public void LogoutRequest_EmptyToken_IsValid()
    {
        LogoutRequest request = new("");

        request.RefreshToken.Should().BeEmpty();
    }

    [Fact]
    public void LogoutRequest_RecordEquality()
    {
        LogoutRequest request1 = new("token-abc");
        LogoutRequest request2 = new("token-abc");

        request1.Should().Be(request2);
    }

    #endregion
}
