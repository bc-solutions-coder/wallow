#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation

namespace Foundry.Identity.Application.Exceptions;

public sealed class ScimFilterException : Exception
{
    public int Position { get; }

    public ScimFilterException()
    {
    }

    public ScimFilterException(string message, int position = -1)
        : base(position >= 0 ? $"{message} at position {position}" : message)
    {
        Position = position;
    }

    public ScimFilterException(string message, int position, Exception innerException)
        : base(position >= 0 ? $"{message} at position {position}" : message, innerException)
    {
        Position = position;
    }

    public ScimFilterException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
