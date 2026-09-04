using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Api.Tests.Problems;

public class ProblemResultTests
{
    private static readonly ErrorCatalogEntry _limitExceeded =
        new("Billing.LimitExceeded", ErrorKind.BusinessRule, "Over limit.");

    [Fact]
    public void Constructor_ExposesAPreliminaryProblemForInspection()
    {
        ProblemResult result = new(404, "Invoice.NotFound", "Invoice was not found.");

        result.StatusCode.Should().Be(404);
        result.Code.Should().Be("Invoice.NotFound");
        ProblemDetails problem = result.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(404);
        problem.Title.Should().Be("Not Found");
        problem.Type.Should().Be("about:blank");
        problem.Detail.Should().Be("Invoice was not found.");
        problem.Extensions["code"].Should().Be("Invoice.NotFound");
    }

    [Fact]
    public void Constructor_WithoutDetail_UsesTheGenericSentence()
    {
        ProblemResult result = new(403, "Auth.Forbidden", null);

        result.Value.Should().BeOfType<ProblemDetails>().Which.Detail.Should().Be(SharedErrors.Forbidden.DefaultMessage);
    }

    [Fact]
    public void ControllerProblem_MapsTheEntryKindToTheStatus()
    {
        ControllerBase controller = Substitute.For<ControllerBase>();

        ProblemResult result = controller.Problem(_limitExceeded);

        result.StatusCode.Should().Be(422);
        result.Code.Should().Be("Billing.LimitExceeded");
        result.Detail.Should().Be("Over limit.");
    }

    [Fact]
    public async Task ExecuteResultAsync_BuildsTheBodyThroughTheFactoryAndTheContract()
    {
        IHostEnvironment hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns("Production");
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(hostEnvironment);
        services.AddMvcCore();
        services.AddWallowProblemDetails();
        ServiceProvider provider = services.BuildServiceProvider();
        DefaultHttpContext httpContext = new() { RequestServices = provider };
        httpContext.Response.Body = new MemoryStream();
        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        ProblemResult result = new(422, "Billing.LimitExceeded", "Over limit.");

        await result.ExecuteResultAsync(actionContext);

        httpContext.Response.StatusCode.Should().Be(422);
        httpContext.Response.ContentType.Should().StartWith(ProblemContract.ContentType);
        httpContext.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        JsonElement body = document.RootElement;
        body.GetProperty("type").GetString().Should().Be("about:blank");
        body.GetProperty("title").GetString().Should().Be("Unprocessable Entity");
        body.GetProperty("status").GetInt32().Should().Be(422);
        body.GetProperty("code").GetString().Should().Be("Billing.LimitExceeded");
        body.GetProperty("detail").GetString().Should().Be("Over limit.");
        body.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        body.TryGetProperty("api", out _).Should().BeFalse();
    }
}
