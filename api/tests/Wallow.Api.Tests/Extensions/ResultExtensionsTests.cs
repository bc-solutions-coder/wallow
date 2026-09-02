using Microsoft.AspNetCore.Mvc;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Api.Tests.Extensions;

public class ResultExtensionsTests
{
    private static readonly ErrorCatalogEntry _businessRule =
        new("Billing.LimitExceeded", ErrorKind.BusinessRule, "Over limit");

    [Fact]
    public void ToActionResult_WhenSuccess_ReturnsOkResult()
    {
        Result result = Result.Success();

        IActionResult actionResult = result.ToActionResult();

        actionResult.Should().BeOfType<OkResult>();
    }

    [Fact]
    public void ToActionResult_WhenFailure_ReturnsProblemDetails()
    {
        Result result = Result.Failure(SharedErrors.NotFound, "Invoice was not found");

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(404);
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(404);
        problem.Title.Should().Be("Not Found");
        problem.Detail.Should().Be("Invoice was not found");
        problem.Extensions["code"].Should().Be("Http.NotFound");
    }

    [Fact]
    public void ToActionResult_WhenFailureWithoutOverride_UsesDefaultSentenceAsDetail()
    {
        Result result = Result.Failure(SharedErrors.NotFound);

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be(SharedErrors.NotFound.DefaultMessage);
    }

    [Fact]
    public void ToActionResultT_WhenSuccess_ReturnsOkObjectResultWithValue()
    {
        string value = "test-value";
        Result<string> result = Result.Success(value);

        IActionResult actionResult = result.ToActionResult();

        OkObjectResult okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(value);
    }

    [Fact]
    public void ToActionResultT_WhenFailure_ReturnsProblemDetails()
    {
        Result<string> result = Result.Failure<string>(SharedErrors.ValidationFailed, "Field is required");

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(400);
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(400);
        problem.Title.Should().Be("Bad Request");
    }

    [Fact]
    public void ToCreatedResult_WithAction_WhenSuccess_ReturnsCreatedAtActionResult()
    {
        Guid id = Guid.NewGuid();
        Result<Guid> result = Result.Success(id);

        IActionResult actionResult = result.ToCreatedResult("GetById", "Invoices", v => new { id = v });

        CreatedAtActionResult created = actionResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be("GetById");
        created.ControllerName.Should().Be("Invoices");
        created.Value.Should().Be(id);
    }

    [Fact]
    public void ToCreatedResult_WithAction_WhenFailure_ReturnsProblemDetails()
    {
        ErrorCatalogEntry conflict = new("Invoice.AlreadyExists", ErrorKind.Conflict, "Already exists");
        Result<Guid> result = Result.Failure<Guid>(conflict);

        IActionResult actionResult = result.ToCreatedResult("GetById", "Invoices", v => new { id = v });

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(409);
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Conflict");
    }

    [Fact]
    public void ToCreatedResult_WithLocation_WhenSuccess_ReturnsCreatedResultWith201()
    {
        string value = "created-item";
        Result<string> result = Result.Success(value);

        IActionResult actionResult = result.ToCreatedResult("/api/items/42");

        CreatedResult created = actionResult.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be("/api/items/42");
        created.Value.Should().Be(value);
    }

    [Fact]
    public void ToCreatedResult_WithLocation_WhenFailure_ReturnsProblemDetails()
    {
        Result<string> result = Result.Failure<string>(SharedErrors.Unauthenticated);

        IActionResult actionResult = result.ToCreatedResult("/api/items/42");

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(401);
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Unauthorized");
    }

    [Fact]
    public void ToNoContentResult_WhenSuccess_ReturnsNoContentResult()
    {
        Result result = Result.Success();

        IActionResult actionResult = result.ToNoContentResult();

        actionResult.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public void ToNoContentResult_WhenFailure_ReturnsProblemDetails()
    {
        Result result = Result.Failure(SharedErrors.Forbidden);

        IActionResult actionResult = result.ToNoContentResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Forbidden");
    }

    [Fact]
    public void ToErrorResult_WithBusinessRuleKind_Returns422WithCode()
    {
        Result result = Result.Failure(_businessRule);

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(422);
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Unprocessable Entity");
        problem.Extensions["code"].Should().Be("Billing.LimitExceeded");
    }

    [Theory]
    [InlineData(ErrorKind.Validation, 400, "Bad Request", "https://tools.ietf.org/html/rfc7231#section-6.5.1")]
    [InlineData(ErrorKind.Unauthenticated, 401, "Unauthorized", "https://tools.ietf.org/html/rfc7235#section-3.1")]
    [InlineData(ErrorKind.Forbidden, 403, "Forbidden", "https://tools.ietf.org/html/rfc7231#section-6.5.3")]
    [InlineData(ErrorKind.NotFound, 404, "Not Found", "https://tools.ietf.org/html/rfc7231#section-6.5.4")]
    [InlineData(ErrorKind.MethodNotAllowed, 405, "Method Not Allowed", "https://tools.ietf.org/html/rfc7231#section-6.5.5")]
    [InlineData(ErrorKind.Conflict, 409, "Conflict", "https://tools.ietf.org/html/rfc7231#section-6.5.8")]
    [InlineData(ErrorKind.BusinessRule, 422, "Unprocessable Entity", "https://tools.ietf.org/html/rfc4918#section-11.2")]
    [InlineData(ErrorKind.RateLimited, 429, "Too Many Requests", "https://tools.ietf.org/html/rfc6585#section-4")]
    [InlineData(ErrorKind.Failure, 500, "Internal Server Error", "https://tools.ietf.org/html/rfc7231#section-6.6.1")]
    [InlineData(ErrorKind.Unavailable, 503, "Service Unavailable", "https://tools.ietf.org/html/rfc7231#section-6.6.4")]
    public void ToErrorResult_DerivesStatusTitleAndTypeFromKind(
        ErrorKind kind,
        int expectedStatus,
        string expectedTitle,
        string expectedType)
    {
        ErrorCatalogEntry entry = new("Test.Code", kind, "test message");
        Result result = Result.Failure(entry);

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatus);
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(expectedStatus);
        problem.Title.Should().Be(expectedTitle);
        problem.Type.Should().Be(expectedType);
        problem.Extensions["code"].Should().Be("Test.Code");
    }

    [Fact]
    public void GetProblemTitle_WithUnmappedStatusCode_ReturnsFallbackError()
    {
        string title = ResultExtensions.GetProblemTitle(418);

        title.Should().Be("Error");
    }

    [Fact]
    public void GetProblemType_WithUnmappedStatusCode_ReturnsFallbackUri()
    {
        string uri = ResultExtensions.GetProblemType(418);

        uri.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.6.1");
    }
}
