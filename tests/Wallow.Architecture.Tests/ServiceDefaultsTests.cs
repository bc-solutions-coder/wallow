using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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
    public void MapDefaultEndpoints_ShouldRegisterHealthEndpoint()
    {
        EndpointDataSource endpointDataSource = _app.Services.GetRequiredService<EndpointDataSource>();

        List<string> routePatterns = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .ToList();

        routePatterns.Should().Contain("/health",
            "MapDefaultEndpoints should register a /health endpoint");
    }

    [Fact]
    public void MapDefaultEndpoints_ShouldRegisterAliveEndpoint()
    {
        EndpointDataSource endpointDataSource = _app.Services.GetRequiredService<EndpointDataSource>();

        List<string> routePatterns = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText ?? string.Empty)
            .ToList();

        routePatterns.Should().Contain("/alive",
            "MapDefaultEndpoints should register an /alive endpoint");
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
