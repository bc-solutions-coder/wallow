using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Tests.Api.Extensions;

public class ResultExtensionsTests
{
    #region ToActionResult (non-generic)

    [Fact]
    public void ToActionResult_WhenSuccess_ReturnsOkResult()
    {
        Result result = Result.Success();

        IActionResult actionResult = result.ToActionResult();

        actionResult.Should().BeOfType<OkResult>();
    }

    [Fact]
    public void ToActionResult_WhenNotFoundError_Returns404()
    {
        Result result = Result.Failure(Error.NotFound("User", "123"));

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ToActionResult_WhenValidationError_Returns400()
    {
        Result result = Result.Failure(Error.Validation("Invalid input"));

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void ToActionResult_WhenUnauthorizedError_Returns401()
    {
        Result result = Result.Failure(Error.Unauthorized());

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void ToActionResult_WhenForbiddenError_Returns403()
    {
        Result result = Result.Failure(Error.Forbidden());

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void ToActionResult_WhenConflictError_Returns409()
    {
        Result result = Result.Failure(Error.Conflict("Already exists"));

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void ToActionResult_WhenUnknownError_Returns422()
    {
        Result result = Result.Failure(new Error("SomeOther.Error", "Something went wrong"));

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public void ToActionResult_ErrorResult_ContainsErrorCodeInExtensions()
    {
        Result result = Result.Failure(Error.NotFound("User", "test"));

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions.Should().ContainKey("code");
        problem.Extensions["code"].Should().Be("User.NotFound");
    }

    [Fact]
    public void ToActionResult_ErrorResult_ContainsErrorMessage()
    {
        Result result = Result.Failure(Error.Validation("Field is required"));

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        ProblemDetails problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Detail.Should().Be("Field is required");
    }

    #endregion

    #region ToActionResult (generic)

    [Fact]
    public void ToActionResult_Generic_WhenSuccess_ReturnsOkObjectResult()
    {
        Result<string> result = Result.Success("hello");

        IActionResult actionResult = result.ToActionResult();

        OkObjectResult ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be("hello");
    }

    [Fact]
    public void ToActionResult_Generic_WhenFailure_ReturnsErrorResult()
    {
        Result<string> result = Result.Failure<string>(Error.NotFound("Item", "123"));

        IActionResult actionResult = result.ToActionResult();

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ToActionResult_Generic_WhenSuccess_ReturnsValueInBody()
    {
        TestDto dto = new("test-id", "Test Name");
        Result<TestDto> result = Result.Success(dto);

        IActionResult actionResult = result.ToActionResult();

        OkObjectResult ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        TestDto responseDto = ok.Value.Should().BeOfType<TestDto>().Subject;
        responseDto.Id.Should().Be("test-id");
        responseDto.Name.Should().Be("Test Name");
    }

    #endregion

    #region ToCreatedResult

    [Fact]
    public void ToCreatedResult_WhenSuccess_ReturnsCreatedResult()
    {
        Result<string> result = Result.Success("created-value");

        IActionResult actionResult = result.ToCreatedResult("/api/identity/users/1");

        CreatedResult created = actionResult.Should().BeOfType<CreatedResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Value.Should().Be("created-value");
        created.Location.Should().Be("/api/identity/users/1");
    }

    [Fact]
    public void ToCreatedResult_WhenFailure_ReturnsErrorResult()
    {
        Result<string> result = Result.Failure<string>(Error.Validation("Invalid data"));

        IActionResult actionResult = result.ToCreatedResult("/api/identity/users/1");

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void ToCreatedResult_WhenConflict_Returns409()
    {
        Result<string> result = Result.Failure<string>(Error.Conflict("Already exists"));

        IActionResult actionResult = result.ToCreatedResult("/api/identity/users/1");

        ObjectResult objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    #endregion

    private sealed record TestDto(string Id, string Name);
}
