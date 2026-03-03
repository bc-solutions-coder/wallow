using Foundry.Api.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Foundry.Api.Tests.Middleware;

public sealed class SecurityHeadersMiddlewareTests
{
    private readonly IWebHostEnvironment _environment = Substitute.For<IWebHostEnvironment>();

    [Fact]
    public async Task InvokeAsync_SetsXContentTypeOptionsHeader()
    {
        IHeaderDictionary headers = await InvokeAndGetHeaders("Development");

        headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task InvokeAsync_SetsXFrameOptionsHeader()
    {
        IHeaderDictionary headers = await InvokeAndGetHeaders("Development");

        headers["X-Frame-Options"].ToString().Should().Be("DENY");
    }

    [Fact]
    public async Task InvokeAsync_SetsReferrerPolicyHeader()
    {
        IHeaderDictionary headers = await InvokeAndGetHeaders("Development");

        headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task InvokeAsync_SetsPermissionsPolicyHeader()
    {
        IHeaderDictionary headers = await InvokeAndGetHeaders("Development");

        headers["Permissions-Policy"].ToString().Should().Be("camera=(), microphone=(), geolocation=()");
    }

    [Fact]
    public async Task InvokeAsync_SetsContentSecurityPolicyHeader()
    {
        IHeaderDictionary headers = await InvokeAndGetHeaders("Development");

        headers["Content-Security-Policy"].ToString().Should().Be("default-src 'self'");
    }

    [Fact]
    public async Task InvokeAsync_InProduction_SetsStrictTransportSecurityHeader()
    {
        IHeaderDictionary headers = await InvokeAndGetHeaders("Production");

        headers["Strict-Transport-Security"].ToString()
            .Should().Be("max-age=31536000; includeSubDomains");
    }

    [Fact]
    public async Task InvokeAsync_NotInProduction_DoesNotSetStrictTransportSecurityHeader()
    {
        IHeaderDictionary headers = await InvokeAndGetHeaders("Development");

        headers.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_CallsNextDelegate()
    {
        _environment.EnvironmentName.Returns("Development");
        bool nextCalled = false;
        SecurityHeadersMiddleware sut = new(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, _environment);
        DefaultHttpContext httpContext = new();

        await sut.InvokeAsync(httpContext);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_InProduction_SetsAllSecurityHeaders()
    {
        IHeaderDictionary headers = await InvokeAndGetHeaders("Production");

        headers.Should().ContainKey("X-Content-Type-Options");
        headers.Should().ContainKey("X-Frame-Options");
        headers.Should().ContainKey("Referrer-Policy");
        headers.Should().ContainKey("Permissions-Policy");
        headers.Should().ContainKey("Content-Security-Policy");
        headers.Should().ContainKey("Strict-Transport-Security");
    }

    private async Task<IHeaderDictionary> InvokeAndGetHeaders(string environmentName)
    {
        _environment.EnvironmentName.Returns(environmentName);
        CallbackCapturingResponseFeature responseFeature = new CallbackCapturingResponseFeature();
        FeatureCollection features = new FeatureCollection();
        features.Set<IHttpResponseFeature>(responseFeature);
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));
        features.Set<IHttpRequestFeature>(new HttpRequestFeature { Path = "/" });
        DefaultHttpContext httpContext = new(features);

        SecurityHeadersMiddleware sut = new(_ => Task.CompletedTask, _environment);
        await sut.InvokeAsync(httpContext);

        await responseFeature.FireOnStartingAsync();
        return httpContext.Response.Headers;
    }

    private sealed class CallbackCapturingResponseFeature : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = [];

        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = Stream.Null;
        public bool HasStarted => false;

        public void OnStarting(Func<object, Task> callback, object state)
        {
            _onStarting.Add((callback, state));
        }

        public void OnCompleted(Func<object, Task> callback, object state) { }

        public async Task FireOnStartingAsync()
        {
            // Fire in reverse order, matching ASP.NET Core behavior
            for (int i = _onStarting.Count - 1; i >= 0; i--)
            {
                await _onStarting[i].Callback(_onStarting[i].State);
            }
        }
    }
}
