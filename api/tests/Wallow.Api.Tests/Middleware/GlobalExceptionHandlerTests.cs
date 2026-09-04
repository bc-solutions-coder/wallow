using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Api.Middleware;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Api.Tests.Middleware;

public class GlobalExceptionHandlerTests
{
    private static readonly ErrorCatalogEntry _invoiceNotFound =
        new("Invoice.NotFound", ErrorKind.NotFound, "Invoice was not found.");

    private static readonly ErrorCatalogEntry _invoiceAlreadyPaid =
        new("Billing.InvoiceAlreadyPaid", ErrorKind.BusinessRule, "Invoice has already been paid.");

    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();
    private readonly GlobalExceptionHandler _sut = new(NullLogger<GlobalExceptionHandler>.Instance);

    public GlobalExceptionHandlerTests()
    {
        _environment.EnvironmentName.Returns("Production");
    }

    [Fact]
    public async Task TryHandleAsync_EntityNotFoundException_Returns404WithTheDomainCodeAndMessage()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        EntityNotFoundException exception = new(_invoiceNotFound, Guid.NewGuid());

        bool handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        JsonElement problem = await ReadProblemAsync(httpContext);
        problem.GetProperty("title").GetString().Should().Be("Not Found");
        problem.GetProperty("status").GetInt32().Should().Be(404);
        problem.GetProperty("code").GetString().Should().Be("Invoice.NotFound");
        problem.GetProperty("detail").GetString().Should().Be(exception.Message);
    }

    [Fact]
    public async Task TryHandleAsync_BusinessRuleException_Returns422WithCode()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        BusinessRuleException exception = new(_invoiceAlreadyPaid);

        await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        JsonElement problem = await ReadProblemAsync(httpContext);
        problem.GetProperty("title").GetString().Should().Be("Unprocessable Entity");
        problem.GetProperty("code").GetString().Should().Be("Billing.InvoiceAlreadyPaid");
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_Returns400WithTheErrorsDictionary()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        ValidationException exception = new(
        [
            new ValidationFailure("Name", "Name is required."),
            new ValidationFailure("Address.City", "City is required."),
            new ValidationFailure("Address.City", "City is too long."),
        ]);

        await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        JsonElement problem = await ReadProblemAsync(httpContext);
        problem.GetProperty("code").GetString().Should().Be("Validation.Failed");
        problem.GetProperty("detail").GetString().Should().Be(SharedErrors.ValidationFailed.DefaultMessage);
        JsonElement errors = problem.GetProperty("errors");
        errors.GetProperty("name")[0].GetString().Should().Be("Name is required.");
        errors.GetProperty("address.city").GetArrayLength().Should().Be(2);
        errors.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(["name", "address.city"]);
    }

    [Fact]
    public async Task TryHandleAsync_UnauthorizedAccessException_Returns401()
    {
        DefaultHttpContext httpContext = CreateHttpContext();

        await _sut.TryHandleAsync(httpContext, new UnauthorizedAccessException("secret"), CancellationToken.None);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        JsonElement problem = await ReadProblemAsync(httpContext);
        problem.GetProperty("code").GetString().Should().Be("Auth.Unauthenticated");
        problem.GetProperty("detail").GetString().Should().Be(SharedErrors.Unauthenticated.DefaultMessage);
    }

    [Fact]
    public async Task TryHandleAsync_ArgumentException_Returns400WithTheClientErrorCode()
    {
        DefaultHttpContext httpContext = CreateHttpContext();

        await _sut.TryHandleAsync(httpContext, new ArgumentNullException("id"), CancellationToken.None);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        JsonElement problem = await ReadProblemAsync(httpContext);
        problem.GetProperty("code").GetString().Should().Be("Http.ClientError");
        problem.GetProperty("detail").GetString().Should().Be(SharedErrors.ClientError.DefaultMessage);
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_Returns500WithTheFixedSentence()
    {
        DefaultHttpContext httpContext = CreateHttpContext();

        await _sut.TryHandleAsync(httpContext, new InvalidOperationException("Something broke"), CancellationToken.None);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        JsonElement problem = await ReadProblemAsync(httpContext);
        problem.GetProperty("title").GetString().Should().Be("Internal Server Error");
        problem.GetProperty("code").GetString().Should().Be("Server.Error");
        problem.GetProperty("detail").GetString().Should().Be(SharedErrors.ServerError.DefaultMessage);
        problem.TryGetProperty("exception", out _).Should().BeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_InDevelopment_KeepsTheFixedSentenceAndAddsTheException()
    {
        _environment.EnvironmentName.Returns("Development");
        DefaultHttpContext httpContext = CreateHttpContext();

        await _sut.TryHandleAsync(httpContext, new InvalidOperationException("Something broke"), CancellationToken.None);

        JsonElement problem = await ReadProblemAsync(httpContext);
        problem.GetProperty("detail").GetString().Should().Be(SharedErrors.ServerError.DefaultMessage);
        problem.GetProperty("exception").GetString().Should().Contain("Something broke");
    }

    [Fact]
    public async Task TryHandleAsync_OperationCanceledByTheClient_Returns499()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        httpContext.RequestAborted = new CancellationToken(canceled: true);

        bool handled = await _sut.TryHandleAsync(httpContext, new OperationCanceledException(), CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(499);
        httpContext.Response.Body.Length.Should().Be(0, "nobody is left to read a body");
    }

    [Fact]
    public async Task TryHandleAsync_OperationCanceledWhileTheClientIsStillConnected_IsAServerError()
    {
        DefaultHttpContext httpContext = CreateHttpContext();

        await _sut.TryHandleAsync(httpContext, new OperationCanceledException(), CancellationToken.None);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        JsonElement problem = await ReadProblemAsync(httpContext);
        problem.GetProperty("code").GetString().Should().Be(SharedErrors.ServerError.Code);
    }

    [Fact]
    public async Task TryHandleAsync_BadHttpRequestException_KeepsTheClientFaultStatus()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        BadHttpRequestException exception = new("Request body too large.", StatusCodes.Status413PayloadTooLarge);

        await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status413PayloadTooLarge);
        JsonElement problem = await ReadProblemAsync(httpContext);
        problem.GetProperty("code").GetString().Should().Be(SharedErrors.ClientError.Code);
        problem.GetProperty("detail").GetString().Should().Be(SharedErrors.ClientError.DefaultMessage);
    }

    [Fact]
    public async Task TryHandleAsync_EveryProblem_HonoursTheContract()
    {
        DefaultHttpContext httpContext = CreateHttpContext();

        await _sut.TryHandleAsync(httpContext, new InvalidOperationException("x"), CancellationToken.None);

        httpContext.Response.ContentType.Should().StartWith(ProblemContract.ContentType);
        JsonElement problem = await ReadProblemAsync(httpContext);
        problem.GetProperty("type").GetString().Should().Be("about:blank");
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        problem.TryGetProperty("api", out _).Should().BeFalse();
        problem.TryGetProperty("version", out _).Should().BeFalse();
        problem.TryGetProperty("instance", out _).Should().BeFalse();
    }

    private DefaultHttpContext CreateHttpContext()
    {
        ServiceProvider provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(_environment)
            .AddWallowProblemDetails()
            .BuildServiceProvider();
        DefaultHttpContext httpContext = new() { RequestServices = provider };
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        return document.RootElement.Clone();
    }
}
