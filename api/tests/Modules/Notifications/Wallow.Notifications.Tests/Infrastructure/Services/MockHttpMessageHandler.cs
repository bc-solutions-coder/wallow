using System.Net;

namespace Wallow.Notifications.Tests.Infrastructure.Services;

public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    public MockHttpMessageHandler(HttpStatusCode statusCode, string content = "{}")
    {
        _response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }
        return _response;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _response.Dispose();
        }
        base.Dispose(disposing);
    }
}

public sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw exception;
    }
}
