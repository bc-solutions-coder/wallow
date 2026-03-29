using Wallow.Shared.Kernel.Results;

namespace Wallow.Shared.Kernel.Tests.Results;

public class ResultTests
{
    [Fact]
    public void Success_ReturnsSuccessfulResult()
    {
        Result result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_ReturnsFailedResult()
    {
        Error error = new("Test.Error", "Test error message");

        Result result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Constructor_SuccessWithNonNoneError_ThrowsInvalidOperationException()
    {
        Error error = new("Test.Error", "Should not be on success");

        Func<TestableResult> act = () => new TestableResult(true, error);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Success result cannot have an error*");
    }

    [Fact]
    public void Constructor_FailureWithNoneError_ThrowsInvalidOperationException()
    {
        Func<TestableResult> act = () => new TestableResult(false, Error.None);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Failure result must have an error*");
    }

    [Fact]
    public void SuccessGeneric_ReturnsSuccessWithValue()
    {
        Result<string> result = Result.Success("test");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("test");
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void FailureGeneric_ReturnsFailureWithError()
    {
        Error error = new("Test.Error", "Test");

        Result<string> result = Result.Failure<string>(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    private sealed class TestableResult(bool isSuccess, Error error) : Result(isSuccess, error);
}

public class ResultOfTTests
{
    [Fact]
    public void Success_WithValue_ReturnsSuccessfulResultWithValue()
    {
        Result<int> result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_AccessingValue_ThrowsInvalidOperationException()
    {
        Result<int> result = Result.Failure<int>(Error.NullValue);

        Func<int> act = () => result.Value;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*failed result*");
    }

    [Fact]
    public void ImplicitConversion_FromValue_ReturnsSuccess()
    {
        Result<string> result = "hello";

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public void Map_WhenSuccess_TransformsValue()
    {
        Result<int> result = Result.Success(5);

        Result<int> mapped = result.Map(x => x * 2);

        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public void Map_WhenFailure_PropagatesError()
    {
        Error error = new("Test.Error", "Test");
        Result<int> result = Result.Failure<int>(error);

        Result<int> mapped = result.Map(x => x * 2);

        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be(error);
    }

    [Fact]
    public void Bind_WhenSuccess_ChainsOperation()
    {
        Result<int> result = Result.Success(5);

        Result<string> bound = result.Bind(x => Result.Success(x.ToString()));

        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("5");
    }

    [Fact]
    public void Bind_WhenFirstFails_PropagatesError()
    {
        Error error = new("Test.Error", "Test");
        Result<int> result = Result.Failure<int>(error);

        Result<string> bound = result.Bind(x => Result.Success(x.ToString()));

        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be(error);
    }

    [Fact]
    public void Bind_WhenSecondFails_ReturnsSecondError()
    {
        Result<int> result = Result.Success(5);
        Error secondError = new("Second.Error", "Second failed");

        Result<string> bound = result.Bind<string>(_ => Result.Failure<string>(secondError));

        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be(secondError);
    }
}
