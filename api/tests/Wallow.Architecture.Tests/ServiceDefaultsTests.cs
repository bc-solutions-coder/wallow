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


        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;

        builder.AddServiceDefaults();

        _app = builder.Build();
        _app.MapDefaultEndpoints();
    }

    [Fact]
    public void AddServiceDefaults_ShouldRegisterServiceDiscovery()
    {
        // Resolve the discovery service by its assembly-qualified name.
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
        // Hosts own detailed health routes; defaults supply the liveness route.
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

        WebApplicationBuilder freshBuilder = WebApplication.CreateBuilder();
        freshBuilder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;


        Action act = () => freshBuilder.AddServiceDefaults();


        act.Should().NotThrow(
            "AddServiceDefaults should not throw when OTEL_EXPORTER_OTLP_ENDPOINT is absent from config");
    }

    public void Dispose()
    {
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
