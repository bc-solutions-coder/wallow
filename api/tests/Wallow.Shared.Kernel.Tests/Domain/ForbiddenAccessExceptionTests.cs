using Wallow.Shared.Kernel.Domain;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Shared.Kernel.Tests.Domain;

public class ForbiddenAccessExceptionTests
{
    [Fact]
    public void Constructor_FromSharedForbidden_UsesKernelCode()
    {
        ForbiddenAccessException exception = new(SharedErrors.Forbidden);

        exception.Code.Should().Be("Auth.Forbidden");
        exception.Kind.Should().Be(ErrorKind.Forbidden);
        exception.Message.Should().Be(SharedErrors.Forbidden.DefaultMessage);
    }

    [Fact]
    public void Constructor_WithOverride_ReplacesMessage()
    {
        ForbiddenAccessException exception = new(SharedErrors.Forbidden, "Only owners may do this.");

        exception.Message.Should().Be("Only owners may do this.");
    }

    [Fact]
    public void Constructor_RefusesNonForbiddenEntry()
    {
        Func<ForbiddenAccessException> act = () => new ForbiddenAccessException(SharedErrors.NotFound);

        act.Should().Throw<ArgumentException>().WithMessage("*Forbidden*");
    }
}
