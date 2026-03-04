namespace Foundry.Shared.Kernel.Domain;

/// <summary>
/// Exception thrown when an external service call fails.
/// </summary>
public class ExternalServiceException : Exception
{
    public int StatusCode { get; }
    public string? ResponseBody { get; }

    public ExternalServiceException(string message, int statusCode, string? responseBody = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public ExternalServiceException()
    {
    }

    public ExternalServiceException(string message) : base(message)
    {
    }

    public ExternalServiceException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
