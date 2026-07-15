using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Wallow.Api.Extensions;

namespace Wallow.Api.Tests.Extensions;

public class AsyncApiEndpointExtensionsTests
{
    private static List<RouteEndpoint> GetMappedEndpoints(WebApplication app)
    {
        IEndpointRouteBuilder routeBuilder = app;
        return routeBuilder.DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();
    }

    private static WebApplication CreateDevAppWithAsyncApi()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();
        app.MapAsyncApiEndpoints();
        return app;
    }

    [Fact]
    public void MapAsyncApiEndpoints_InDevelopment_MapsExpectedRoutes()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        WebApplication app = builder.Build();

        app.MapAsyncApiEndpoints();

        List<string?> patterns = GetMappedEndpoints(app)
            .Select(e => e.RoutePattern.RawText)
            .ToList();

        patterns.Should().Contain("/asyncapi/v1.json");
        patterns.Should().Contain("/asyncapi/v1/flows");
        patterns.Should().Contain("/asyncapi");
    }

    [Fact]
    public void MapAsyncApiEndpoints_NotInDevelopment_DoesNotMapRoutes()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production"
        });
        WebApplication app = builder.Build();

        app.MapAsyncApiEndpoints();

        List<string?> patterns = GetMappedEndpoints(app)
            .Select(e => e.RoutePattern.RawText)
            .ToList();

        patterns.Should().NotContain("/asyncapi/v1.json");
        patterns.Should().NotContain("/asyncapi/v1/flows");
        patterns.Should().NotContain("/asyncapi");
    }

    [Fact]
    public void MapAsyncApiEndpoints_ReturnsWebApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        WebApplication app = builder.Build();

        WebApplication result = app.MapAsyncApiEndpoints();

        result.Should().BeSameAs(app);
    }

    [Fact]
    public void MapAsyncApiEndpoints_NotInDevelopment_ReturnsWebApplication()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production"
        });
        WebApplication app = builder.Build();

        WebApplication result = app.MapAsyncApiEndpoints();

        result.Should().BeSameAs(app);
    }

    [Fact]
    public void MapAsyncApiEndpoints_Endpoints_AreExcludedFromDescription()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        WebApplication app = builder.Build();

        app.MapAsyncApiEndpoints();

        List<RouteEndpoint> asyncApiEndpoints = GetMappedEndpoints(app)
            .Where(e => e.RoutePattern.RawText is "/asyncapi/v1.json" or "/asyncapi/v1/flows" or "/asyncapi")
            .ToList();

        asyncApiEndpoints.Should().HaveCount(3);
        foreach (RouteEndpoint endpoint in asyncApiEndpoints)
        {
            endpoint.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>()
                .Should().NotBeNull($"endpoint {endpoint.RoutePattern.RawText} should be excluded from description");
        }
    }

    [Fact]
    public async Task AsyncApiJsonEndpoint_ReturnsJsonDocument()
    {
        await using WebApplication app = CreateDevAppWithAsyncApi();
        await app.StartAsync();
        HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.GetAsync("/asyncapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("asyncapi");
    }

    [Fact]
    public async Task AsyncApiFlowsEndpoint_ReturnsMermaidText()
    {
        await using WebApplication app = CreateDevAppWithAsyncApi();
        await app.StartAsync();
        HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.GetAsync("/asyncapi/v1/flows");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
    }

    [Fact]
    public async Task AsyncApiViewerEndpoint_ReturnsHtml()
    {
        await using WebApplication app = CreateDevAppWithAsyncApi();
        await app.StartAsync();
        HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.GetAsync("/asyncapi");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Wallow AsyncAPI");
        body.Should().Contain("asyncapi/v1.json");
    }
}
