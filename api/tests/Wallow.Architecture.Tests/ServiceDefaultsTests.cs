using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Wallow.ServiceDefaults;

namespace Wallow.Architecture.Tests;

public sealed class ServiceDefaultsTests : IDisposable
{
    private readonly WebApplication _app;

    public ServiceDefaultsTests()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Ensure no OTEL endpoint is configured
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;

        builder.AddServiceDefaults();

        _app = builder.Build();
        _app.MapDefaultEndpoints();
    }

    [Fact]
    public void AddServiceDefaults_ShouldRegisterServiceDiscovery()
    {
        // Service discovery registers ServiceEndpointWatcherFactory in the DI container.
        // We look for it by name because the test project does not reference the ServiceDiscovery package directly.
        Type? serviceDiscoveryType = Type.GetType(
            "Microsoft.Extensions.ServiceDiscovery.ServiceEndpointWatcherFactory, Microsoft.Extensions.ServiceDiscovery");

        serviceDiscoveryType.Should().NotBeNull(
            "the ServiceDiscovery assembly should be loaded when AddServiceDefaults registers service discovery");

        object? serviceDiscovery = _app.Services.GetService(serviceDiscoveryType!);

        serviceDiscovery.Should().NotBeNull(
            "AddServiceDefaults should register service discovery in the DI container");
    }

    [Fact]
    public void MapDefaultEndpoints_ShouldNotRegisterHealthEndpoint()
    {
        // Health checks are now mapped by each app's Program.cs with custom response writers,
        // not by MapDefaultEndpoints.
        List<string> routePatterns = GetRoutePatterns();

        routePatterns.Should().NotContain(p => p.Contains("health"),
            "MapDefaultEndpoints should not register /health — each app maps its own health endpoint");
    }

    [Fact]
    public void MapDefaultEndpoints_ShouldRegisterAliveEndpoint()
    {
        List<string> routePatterns = GetRoutePatterns();

        routePatterns.Should().Contain(p => p.Contains("alive"),
            "MapDefaultEndpoints should register an /alive endpoint via minimal API");
    }

    private List<string> GetRoutePatterns()
    {
        // Minimal API endpoints are registered on the app's own data sources,
        // which are accessed via the IEndpointRouteBuilder interface.
        IEndpointRouteBuilder routeBuilder = _app;
        return routeBuilder.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .ToList();
    }

    [Fact]
    public void AddServiceDefaults_ShouldNotThrow_WhenOtelEndpointIsAbsent()
    {
        // Arrange - create a fresh builder with no OTEL config
        WebApplicationBuilder freshBuilder = WebApplication.CreateBuilder();
        freshBuilder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;

        // Act
        Action act = () => freshBuilder.AddServiceDefaults();

        // Assert
        act.Should().NotThrow(
            "AddServiceDefaults should not throw when OTEL_EXPORTER_OTLP_ENDPOINT is absent from config");
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
