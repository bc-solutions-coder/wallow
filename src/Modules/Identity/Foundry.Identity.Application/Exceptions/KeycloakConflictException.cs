namespace Foundry.Identity.Application.Exceptions;

public sealed class KeycloakConflictException : Exception
{
    public KeycloakConflictException()
    {
    }

    public KeycloakConflictException(string message) : base(message)
    {
    }

    public KeycloakConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
