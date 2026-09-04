using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Api.Tests.Problems;

public class WallowProblemDetailsWriterTests
{
    [Fact]
    public async Task TryWriteProblemAsync_WritesProblemJsonEvenWhenAcceptAdmitsNoJson()
    {
        (IProblemDetailsService service, DefaultHttpContext httpContext) = Arrange();
        httpContext.Request.Headers.Accept = "text/html";

        bool written = await service.TryWriteProblemAsync(httpContext, SharedErrors.NotFound);

        written.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(404);
        httpContext.Response.ContentType.Should().StartWith(ProblemContract.ContentType);
        JsonElement body = await ReadBodyAsync(httpContext);
        body.GetProperty("type").GetString().Should().Be("about:blank");
        body.GetProperty("title").GetString().Should().Be("Not Found");
        body.GetProperty("status").GetInt32().Should().Be(404);
        body.GetProperty("code").GetString().Should().Be("Http.NotFound");
        body.GetProperty("detail").GetString().Should().Be(SharedErrors.NotFound.DefaultMessage);
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        body.TryGetProperty("instance", out _).Should().BeFalse();
        body.TryGetProperty("errors", out _).Should().BeFalse();
    }

    [Fact]
    public async Task TryWriteProblemAsync_WithEntryAndDetail_UsesTheEntryStatusAndCode()
    {
        (IProblemDetailsService service, DefaultHttpContext httpContext) = Arrange();

        await service.TryWriteProblemAsync(httpContext, SharedErrors.RateLimitExceeded, "Retry after the header.");

        httpContext.Response.StatusCode.Should().Be(429);
        JsonElement body = await ReadBodyAsync(httpContext);
        body.GetProperty("code").GetString().Should().Be("RateLimit.Exceeded");
        body.GetProperty("detail").GetString().Should().Be("Retry after the header.");
    }

    [Fact]
    public async Task TryWriteValidationProblemAsync_KeepsTheErrorsDictionary()
    {
        (IProblemDetailsService service, DefaultHttpContext httpContext) = Arrange();
        Dictionary<string, string[]> errors = new()
        {
            ["Branding.DisplayName"] = ["Display name is required."],
        };

        bool written = await service.TryWriteValidationProblemAsync(httpContext, errors);

        written.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(400);
        JsonElement body = await ReadBodyAsync(httpContext);
        body.GetProperty("code").GetString().Should().Be("Validation.Failed");
        body.GetProperty("detail").GetString().Should().Be(SharedErrors.ValidationFailed.DefaultMessage);
        body.GetProperty("errors").GetProperty("branding.displayName")[0].GetString()
            .Should().Be("Display name is required.");
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    public async Task TryWriteProblemAsync_5xx_ExposesTheExceptionOnlyInDevelopment(string environment, bool exposed)
    {
        (IProblemDetailsService service, DefaultHttpContext httpContext) = Arrange(environment);

        await service.TryWriteProblemAsync(
            httpContext,
            500,
            exception: new InvalidOperationException("Something broke"));

        JsonElement body = await ReadBodyAsync(httpContext);
        body.GetProperty("detail").GetString().Should().Be(SharedErrors.ServerError.DefaultMessage);
        body.GetProperty("code").GetString().Should().Be("Server.Error");
        body.TryGetProperty("exception", out JsonElement exception).Should().Be(exposed);
        if (exposed)
        {
            exception.GetString().Should().Contain("Something broke");
        }
    }

    private static (IProblemDetailsService Service, DefaultHttpContext HttpContext) Arrange(string environment = "Production")
    {
        IHostEnvironment hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns(environment);
        ServiceProvider provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(hostEnvironment)
            .AddWallowProblemDetails()
            .BuildServiceProvider();

        DefaultHttpContext httpContext = new() { RequestServices = provider };
        httpContext.Response.Body = new MemoryStream();

        return (provider.GetRequiredService<IProblemDetailsService>(), httpContext);
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        return document.RootElement.Clone();
    }
}
