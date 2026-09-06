using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Api.Tests.Problems;

public class ProblemContractTests
{
    [Theory]
    [InlineData(400, "Validation.Failed")]
    [InlineData(401, "Auth.Unauthenticated")]
    [InlineData(403, "Auth.Forbidden")]
    [InlineData(404, "Http.NotFound")]
    [InlineData(405, "Http.MethodNotAllowed")]
    [InlineData(409, "Http.ClientError")]
    [InlineData(429, "RateLimit.Exceeded")]
    [InlineData(500, "Server.Error")]
    [InlineData(502, "Server.Error")]
    [InlineData(503, "Server.Error")]
    public void Customize_FillsTheStatusGenericCode(int status, string expectedCode)
    {
        ProblemDetailsContext context = CreateContext(new ProblemDetails { Status = status });

        ProblemContract.Customize(context);

        context.ProblemDetails.Extensions["code"].Should().Be(expectedCode);
    }

    [Fact]
    public void Customize_KeepsAWriterSuppliedCode()
    {
        ProblemDetails problem = new() { Status = 404 };
        problem.Extensions["code"] = "Invoice.NotFound";
        ProblemDetailsContext context = CreateContext(problem);

        ProblemContract.Customize(context);

        problem.Extensions["code"].Should().Be("Invoice.NotFound");
    }

    [Fact]
    public void Customize_ForcesTypeAndTitleAndDropsInstanceApiAndVersion()
    {
        ProblemDetails problem = new()
        {
            Status = 404,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Title = "Nope",
            Instance = "/errors/abc",
        };
        problem.Extensions["api"] = "Wallow";
        problem.Extensions["version"] = "1.0.0";
        ProblemDetailsContext context = CreateContext(problem);

        ProblemContract.Customize(context);

        problem.Type.Should().Be("about:blank");
        problem.Title.Should().Be("Not Found");
        problem.Instance.Should().BeNull();
        problem.Extensions.Should().NotContainKeys("api", "version");
    }

    [Theory]
    [InlineData(200)]
    [InlineData(404)]
    [InlineData(418)]
    [InlineData(499)]
    [InlineData(503)]
    public void Customize_TitleIsTheReasonPhrase(int status)
    {
        ProblemDetailsContext context = CreateContext(new ProblemDetails { Status = status < 400 ? 500 : status });

        ProblemContract.Customize(context);

        context.ProblemDetails.Title.Should().Be(ProblemContract.TitleFor(context.ProblemDetails.Status!.Value));
        context.ProblemDetails.Title.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TitleFor_UnknownStatus_FallsBackToError()
    {
        ProblemContract.TitleFor(299).Should().Be("Error");
        ProblemContract.TitleFor(499).Should().Be("Client Closed Request");
    }

    [Fact]
    public void Customize_TakesTheStatusFromTheResponseWhenTheProblemHasNone()
    {
        ProblemDetailsContext context = CreateContext(new ProblemDetails(), responseStatus: 405);

        ProblemContract.Customize(context);

        context.ProblemDetails.Status.Should().Be(405);
        context.ProblemDetails.Extensions["code"].Should().Be("Http.MethodNotAllowed");
    }

    [Fact]
    public void Customize_DefaultsTo500WhenNeitherTheProblemNorTheResponseIsAnError()
    {
        ProblemDetailsContext context = CreateContext(new ProblemDetails(), responseStatus: 200);

        ProblemContract.Customize(context);

        context.ProblemDetails.Status.Should().Be(500);
    }

    [Fact]
    public void Customize_AddsATraceIdWhenMissing()
    {
        ProblemDetailsContext context = CreateContext(new ProblemDetails { Status = 404 });
        context.HttpContext.TraceIdentifier = "request-trace";

        ProblemContract.Customize(context);

        string expected = Activity.Current?.Id ?? "request-trace";
        context.ProblemDetails.Extensions["traceId"].Should().Be(expected);
    }

    [Fact]
    public void Customize_KeepsAnExistingTraceId()
    {
        ProblemDetails problem = new() { Status = 404 };
        problem.Extensions["traceId"] = "00-abc-def-01";
        ProblemDetailsContext context = CreateContext(problem);

        ProblemContract.Customize(context);

        problem.Extensions["traceId"].Should().Be("00-abc-def-01");
    }

    [Fact]
    public void Customize_FillsAMissing4xxDetailWithTheGenericSentence()
    {
        ProblemDetailsContext context = CreateContext(new ProblemDetails { Status = 403 });

        ProblemContract.Customize(context);

        context.ProblemDetails.Detail.Should().Be(SharedErrors.Forbidden.DefaultMessage);
    }

    [Fact]
    public void Customize_KeepsAWriterSupplied4xxDetail()
    {
        ProblemDetailsContext context = CreateContext(new ProblemDetails { Status = 404, Detail = "Invoice was not found." });

        ProblemContract.Customize(context);

        context.ProblemDetails.Detail.Should().Be("Invoice was not found.");
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void Customize_Forces5xxDetailToTheFixedSentenceInEveryEnvironment(string environment)
    {
        ProblemDetailsContext context = CreateContext(
            new ProblemDetails { Status = 500, Detail = "NullReferenceException at Foo.Bar" },
            environment,
            new InvalidOperationException("Something broke"));

        ProblemContract.Customize(context);

        context.ProblemDetails.Detail.Should().Be(SharedErrors.ServerError.DefaultMessage);
    }

    [Theory]
    [InlineData("Development", 500, true)]
    [InlineData("Development", 503, true)]
    [InlineData("Production", 500, false)]
    [InlineData("Development", 400, false)]
    public void Customize_ExposesTheExceptionOnlyInDevelopmentOn5xx(string environment, int status, bool expected)
    {
        ProblemDetailsContext context = CreateContext(
            new ProblemDetails { Status = status },
            environment,
            new InvalidOperationException("Something broke"));

        ProblemContract.Customize(context);

        context.ProblemDetails.Extensions.ContainsKey("exception").Should().Be(expected);
        if (expected)
        {
            context.ProblemDetails.Extensions["exception"].Should().BeOfType<string>()
                .Which.Should().Contain("Something broke");
        }
    }

    [Fact]
    public void Customize_ValidationProblem_SetsCodeAndDetailAndCamelCasesDotPreservingKeys()
    {
        HttpValidationProblemDetails problem = new(new Dictionary<string, string[]>
        {
            ["Branding.DisplayName"] = ["Display name is required."],
            ["branding.displayName"] = ["Display name is reserved."],
            ["Name"] = ["Name is required."],
            [string.Empty] = ["The body is required."],
        })
        {
            Status = 400,
        };
        problem.Extensions["code"] = "Something.Else";
        ProblemDetailsContext context = CreateContext(problem);

        ProblemContract.Customize(context);

        problem.Extensions["code"].Should().Be("Validation.Failed");
        problem.Detail.Should().Be(SharedErrors.ValidationFailed.DefaultMessage);
        problem.Errors.Keys.Should().BeEquivalentTo(["branding.displayName", "name", string.Empty]);
        problem.Errors["branding.displayName"].Should().Equal("Display name is required.", "Display name is reserved.");
        problem.Errors["name"].Should().Equal("Name is required.");
    }

    [Theory]
    [InlineData("DisplayName", "displayName")]
    [InlineData("displayName", "displayName")]
    [InlineData("Branding.DisplayName", "branding.displayName")]
    [InlineData("branding.DisplayName", "branding.displayName")]
    [InlineData("Items[0].Name", "items[0].name")]
    [InlineData("URL", "url")]
    [InlineData("", "")]
    public void NormalizeValidationKey_CamelCasesEachSegment(string key, string expected)
    {
        ProblemContract.NormalizeValidationKey(key).Should().Be(expected);
    }

    [Fact]
    public void Customize_IsIdempotent()
    {
        HttpValidationProblemDetails problem = new(new Dictionary<string, string[]>
        {
            ["Branding.DisplayName"] = ["Display name is required."],
        });
        ProblemDetailsContext context = CreateContext(problem, "Development", new InvalidOperationException("x"));

        ProblemContract.Customize(context);
        string once = JsonSerializer.Serialize(problem, problem.GetType());
        ProblemContract.Customize(context);
        string twice = JsonSerializer.Serialize(problem, problem.GetType());

        twice.Should().Be(once);
    }

    private static ProblemDetailsContext CreateContext(
        ProblemDetails problem,
        string environment = "Production",
        Exception? exception = null,
        int responseStatus = 200)
    {
        IHostEnvironment hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns(environment);
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(hostEnvironment)
            .BuildServiceProvider();
        DefaultHttpContext httpContext = new() { RequestServices = provider };
        httpContext.Response.StatusCode = responseStatus;

        return new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        };
    }
}
