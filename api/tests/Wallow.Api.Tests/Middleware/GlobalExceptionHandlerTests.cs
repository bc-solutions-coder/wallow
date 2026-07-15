using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Wallow.Api.Middleware;
using Wallow.Shared.Kernel.Domain;

namespace Wallow.Api.Tests.Middleware;

public class GlobalExceptionHandlerTests
{
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();
    private readonly GlobalExceptionHandler _sut;

    public GlobalExceptionHandlerTests()
    {
        _environment.EnvironmentName.Returns("Production");
        _sut = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            _environment);
    }

    [Fact]
    public async Task TryHandleAsync_EntityNotFoundException_Returns404WithCode()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        EntityNotFoundException exception = new("Invoice", Guid.NewGuid());

        bool handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        ProblemDetails problem = await ReadProblemDetails(httpContext);
        problem.Title.Should().Be("Resource Not Found");
        problem.Status.Should().Be(404);
        problem.Extensions.Should().ContainKey("code")
            .WhoseValue.Should().NotBeNull();
    }

    [Fact]
    public async Task TryHandleAsync_BusinessRuleException_Returns422WithCode()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        BusinessRuleException exception = new("Billing.InvoiceAlreadyPaid", "Invoice has already been paid");

        bool handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        ProblemDetails problem = await ReadProblemDetails(httpContext);
        problem.Title.Should().Be("Business Rule Violation");
        problem.Status.Should().Be(422);
        problem.Detail.Should().Be("Invoice has already been paid");
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_Returns400WithErrors()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        List<ValidationFailure> failures =
        [
            new("Name", "Name is required"),
            new("Email", "Email is invalid")
        ];
        ValidationException exception = new(failures);

        bool handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ProblemDetails problem = await ReadProblemDetails(httpContext);
        problem.Title.Should().Be("Validation Error");
        problem.Status.Should().Be(400);
        problem.Detail.Should().Contain("Name is required");
        problem.Detail.Should().Contain("Email is invalid");
        problem.Extensions.Should().ContainKey("errors");
    }

    [Fact]
    public async Task TryHandleAsync_UnauthorizedAccessException_Returns401()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        UnauthorizedAccessException exception = new("Access denied");

        bool handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        ProblemDetails problem = await ReadProblemDetails(httpContext);
        problem.Title.Should().Be("Unauthorized");
        problem.Status.Should().Be(401);
    }

    [Fact]
    public async Task TryHandleAsync_ArgumentException_Returns400()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        ArgumentException exception = new("Value cannot be empty");

        bool handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        ProblemDetails problem = await ReadProblemDetails(httpContext);
        problem.Title.Should().Be("Bad Request");
        problem.Status.Should().Be(400);
    }

    [Fact]
    public async Task TryHandleAsync_GenericException_Returns500()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        InvalidOperationException exception = new("Something broke");

        bool handled = await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        ProblemDetails problem = await ReadProblemDetails(httpContext);
        problem.Title.Should().Be("Internal Server Error");
        problem.Status.Should().Be(500);
        problem.Detail.Should().Be("An unexpected error occurred. Please try again later.");
    }

    [Fact]
    public async Task TryHandleAsync_GenericExceptionInDevelopment_ExposesExceptionDetails()
    {
        _environment.EnvironmentName.Returns("Development");
        GlobalExceptionHandler devHandler = new(
            NullLogger<GlobalExceptionHandler>.Instance,
            _environment);
        DefaultHttpContext httpContext = CreateHttpContext();
        InvalidOperationException exception = new("Something broke");

        bool handled = await devHandler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        handled.Should().BeTrue();
        ProblemDetails problem = await ReadProblemDetails(httpContext);
        problem.Detail.Should().Be("Something broke");
        problem.Extensions.Should().ContainKey("exception");
    }

    [Fact]
    public async Task TryHandleAsync_BusinessRuleException_IncludesCodeInExtensions()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        BusinessRuleException exception = new("Billing.InvoiceAlreadyPaid", "Invoice has already been paid");

        await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        ProblemDetails problem = await ReadProblemDetails(httpContext);
        problem.Extensions.Should().ContainKey("code")
            .WhoseValue!.ToString().Should().Be("Billing.InvoiceAlreadyPaid");
    }

    [Fact]
    public async Task TryHandleAsync_EntityNotFoundException_IncludesEntityNotFoundCode()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        Guid entityId = Guid.NewGuid();
        EntityNotFoundException exception = new("Invoice", entityId);

        await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        ProblemDetails problem = await ReadProblemDetails(httpContext);
        problem.Extensions.Should().ContainKey("code")
            .WhoseValue!.ToString().Should().Be("Invoice.NotFound");
    }

    [Fact]
    public async Task TryHandleAsync_DomainExceptionInProduction_StillExposesDetailMessage()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        BusinessRuleException exception = new("Billing.LimitExceeded", "Monthly limit exceeded");

        await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        ProblemDetails problem = await ReadProblemDetails(httpContext);
        problem.Detail.Should().Be("Monthly limit exceeded");
    }

    [Fact]
    public async Task TryHandleAsync_GenericExceptionInProduction_ExcludesExceptionExtension()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        InvalidOperationException exception = new("Something broke");

        await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        ProblemDetails problem = await ReadProblemDetails(httpContext);
        problem.Extensions.Should().NotContainKey("exception");
        problem.Detail.Should().Be("An unexpected error occurred. Please try again later.");
    }

    [Fact]
    public async Task TryHandleAsync_GenericExceptionInDevelopment_IncludesStackTraceInExceptionExtension()
    {
        _environment.EnvironmentName.Returns("Development");
        GlobalExceptionHandler devHandler = new(
            NullLogger<GlobalExceptionHandler>.Instance,
            _environment);
        DefaultHttpContext httpContext = CreateHttpContext();
        InvalidOperationException exception;
        try { throw new InvalidOperationException("Something broke"); }
        catch (InvalidOperationException ex) { exception = ex; }

        await devHandler.TryHandleAsync(httpContext, exception, CancellationToken.None);

        ProblemDetails problem = await ReadProblemDetails(httpContext);
        string exceptionText = problem.Extensions["exception"]!.ToString()!;
        exceptionText.Should().Contain("Something broke");
        exceptionText.Should().Contain("GlobalExceptionHandlerTests");
    }

    [Fact]
    public async Task TryHandleAsync_Always_IncludesTraceId()
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        ArgumentException exception = new("bad input");

        await _sut.TryHandleAsync(httpContext, exception, CancellationToken.None);

        ProblemDetails problem = await ReadProblemDetails(httpContext);
        problem.Extensions.Should().ContainKey("traceId");
        problem.Instance.Should().StartWith("/errors/");
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static async Task<ProblemDetails> ReadProblemDetails(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        ProblemDetails? problem = await JsonSerializer.DeserializeAsync<ProblemDetails>(
            context.Response.Body, _jsonOptions);
        return problem!;
    }
}
