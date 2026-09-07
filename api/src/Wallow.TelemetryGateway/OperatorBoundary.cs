using System.Net;
using Microsoft.AspNetCore.Http.Features;

namespace Wallow.TelemetryGateway;

public static class OperatorBoundary
{
    private static readonly string[] _responseHeaders = ["Content-Type", "Cache-Control", "Content-Encoding", "Location", "Content-Security-Policy", "X-Content-Type-Options"];
    public static WebApplication Create(Uri grafana, IPAddress peer, string identityHeader, IReadOnlyDictionary<string, string> operators)
    {
        ArgumentNullException.ThrowIfNull(grafana);
        ArgumentNullException.ThrowIfNull(peer);
        ArgumentNullException.ThrowIfNull(operators);
        if (operators.Count == 0 || identityHeader.Length is < 1 or > 128
            || !identityHeader.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            || operators.Any(pair => !SafeIdentity(pair.Key) || !SafeIdentity(pair.Value)))
        {
            throw new ArgumentException("Explicit operator identities and a proxy identity header are required", nameof(operators));
        }
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 1024 * 1024;
            options.Limits.MaxConcurrentConnections = 32;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddHttpClient("grafana", client =>
        {
            client.BaseAddress = grafana;
            client.Timeout = TimeSpan.FromSeconds(30);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
        WebApplication app = builder.Build();
        app.Run(async context =>
        {
            if (!Equals(context.Connection.RemoteIpAddress?.MapToIPv6(), peer.MapToIPv6())
                || !operators.TryGetValue(context.Request.Headers[identityHeader].ToString(), out string? user))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            UriBuilder destination = new(grafana) { Path = context.Request.Path, Query = context.Request.QueryString.Value };
            using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            budget.CancelAfter(TimeSpan.FromSeconds(30));
            using HttpRequestMessage request = new(new HttpMethod(context.Request.Method), destination.Uri);
            request.Headers.Add("X-WEBAUTH-USER", user);
            request.Headers.TryAddWithoutValidation("Accept", context.Request.Headers.Accept.ToArray());
            request.Headers.TryAddWithoutValidation("Origin", context.Request.Headers.Origin.ToArray());
            if (context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true)
            {
                request.Content = new StreamContent(context.Request.Body);
                request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
            }
            using HttpClient client = context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient("grafana");
            try
            {
                using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token).ConfigureAwait(false);
                context.Response.StatusCode = (int)response.StatusCode;
                foreach (string header in _responseHeaders)
                {
                    if (response.Headers.TryGetValues(header, out IEnumerable<string>? values) || response.Content.Headers.TryGetValues(header, out values))
                    {
                        context.Response.Headers[header] = values.ToArray();
                    }
                }
                await response.Content.CopyToAsync(context.Response.Body, budget.Token).ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
            {
                if (!context.Response.HasStarted) { context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable; }
            }
        });
        return app;
    }

    private static bool SafeIdentity(string value) => value.Length is > 0 and <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '@');
}
