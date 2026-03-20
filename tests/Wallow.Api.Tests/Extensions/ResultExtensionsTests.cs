using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace Wallow.Api.Tests.Extensions;

public class ResultExtensionsTests
{
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
        Result result = Result.Failure(Error.NotFound("Invoice", Guid.NewGuid()));

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(404);
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(404);
        problem.Title.Should().Be("Not Found");
        problem.Detail.Should().Contain("Invoice");
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
        Result<string> result = Result.Failure<string>(Error.Validation("Field is required"));

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
        Result<Guid> result = Result.Failure<Guid>(Error.Conflict("Already exists"));

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
        Result<string> result = Result.Failure<string>(Error.Unauthorized());

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
        Result result = Result.Failure(Error.Forbidden());

        IActionResult actionResult = result.ToNoContentResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Forbidden");
    }

    [Fact]
    public void ToErrorResult_WithDefaultErrorCode_Returns422()
    {
        Result result = Result.Failure(Error.BusinessRule("LimitExceeded", "Over limit"));

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(422);
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Unprocessable Entity");
        problem.Extensions["code"].Should().Be("BusinessRule.LimitExceeded");
    }

    [Theory]
    [InlineData("Invoice.NotFound", 404)]
    [InlineData("User.NotFound", 404)]
    [InlineData("Validation.Error", 400)]
    [InlineData("ValidationFailed", 400)]
    [InlineData("Unauthorized.Error", 401)]
    [InlineData("UnauthorizedAccess", 401)]
    [InlineData("Forbidden.Error", 403)]
    [InlineData("ForbiddenOperation", 403)]
    [InlineData("Conflict.Error", 409)]
    [InlineData("ConflictDetected", 409)]
    [InlineData("BusinessRule.Custom", 422)]
    [InlineData("SomeOther.Code", 422)]
    public void GetStatusCode_WithErrorCode_ReturnsExpectedHttpStatus(string errorCode, int expectedStatus)
    {
        Result result = Result.Failure(new Error(errorCode, "test message"));

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatus);
    }

    [Theory]
    [InlineData("Invoice.NotFound", "Not Found")]
    [InlineData("Validation.Error", "Bad Request")]
    [InlineData("Unauthorized.Error", "Unauthorized")]
    [InlineData("Forbidden.Error", "Forbidden")]
    [InlineData("Conflict.Error", "Conflict")]
    [InlineData("BusinessRule.Custom", "Unprocessable Entity")]
    public void GetTitle_WithErrorCode_ReturnsExpectedTitle(string errorCode, string expectedTitle)
    {
        Result result = Result.Failure(new Error(errorCode, "test message"));

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be(expectedTitle);
    }

    [Theory]
    [InlineData("Invoice.NotFound", "https://tools.ietf.org/html/rfc7231#section-6.5.4")]
    [InlineData("Validation.Error", "https://tools.ietf.org/html/rfc7231#section-6.5.1")]
    [InlineData("Unauthorized.Error", "https://tools.ietf.org/html/rfc7235#section-3.1")]
    [InlineData("Forbidden.Error", "https://tools.ietf.org/html/rfc7231#section-6.5.3")]
    [InlineData("Conflict.Error", "https://tools.ietf.org/html/rfc7231#section-6.5.8")]
    [InlineData("BusinessRule.Custom", "https://tools.ietf.org/html/rfc4918#section-11.2")]
    public void GetTypeUri_WithErrorCode_ReturnsExpectedRfcUri(string errorCode, string expectedUri)
    {
        Result result = Result.Failure(new Error(errorCode, "test message"));

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Type.Should().Be(expectedUri);
    }

    [Fact]
    public void GetTitle_WithUnmappedStatusCode_ReturnsFallbackError()
    {
        string title = ResultExtensions.GetTitle(418);

        title.Should().Be("Error");
    }

    [Fact]
    public void GetTypeUri_WithUnmappedStatusCode_ReturnsFallbackUri()
    {
        string uri = ResultExtensions.GetTypeUri(418);

        uri.Should().Be("https://tools.ietf.org/html/rfc7231#section-6.6.1");
    }
}
