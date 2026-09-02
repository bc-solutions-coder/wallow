using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Kernel.Tests.Errors;

public class ErrorKindExtensionsTests
{
    [Theory]
    [InlineData(ErrorKind.Validation, 400)]
    [InlineData(ErrorKind.Unauthenticated, 401)]
    [InlineData(ErrorKind.Forbidden, 403)]
    [InlineData(ErrorKind.NotFound, 404)]
    [InlineData(ErrorKind.MethodNotAllowed, 405)]
    [InlineData(ErrorKind.Conflict, 409)]
    [InlineData(ErrorKind.BusinessRule, 422)]
    [InlineData(ErrorKind.RateLimited, 429)]
    [InlineData(ErrorKind.Failure, 500)]
    [InlineData(ErrorKind.Unavailable, 503)]
    public void ToHttpStatusCode_MapsEveryKind(ErrorKind kind, int expected)
    {
        kind.ToHttpStatusCode().Should().Be(expected);
    }

    [Fact]
    public void ToHttpStatusCode_CoversEveryDefinedKind()
    {
        foreach (ErrorKind kind in Enum.GetValues<ErrorKind>())
        {
            Func<int> act = () => kind.ToHttpStatusCode();

            act.Should().NotThrow();
        }
    }

    [Fact]
    public void ToHttpStatusCode_RejectsUndefinedKind()
    {
        Func<int> act = () => ((ErrorKind)999).ToHttpStatusCode();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
