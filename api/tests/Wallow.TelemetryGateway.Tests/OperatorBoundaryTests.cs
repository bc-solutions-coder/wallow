using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Wallow.TelemetryGateway.Tests;

public sealed class OperatorBoundaryTests
{
    [Fact]
    public async Task OnlyVerifiedPeerAndExplicitOperatorReachGrafana_WithReplacedHeaders()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using WebApplication grafana = builder.Build();
        grafana.MapGet("/api/user", (HttpContext context) => Results.Json(new
        {
            user = context.Request.Headers["X-WEBAUTH-USER"].ToString(),
            role = context.Request.Headers["X-WEBAUTH-ROLE"].ToString(),
            authorization = context.Request.Headers.Authorization.ToString(),
            cookie = context.Request.Headers.Cookie.ToString(),
        }));
        await grafana.StartAsync();
        Dictionary<string, string> operators = new() { ["verified-operator"] = "observability-viewer" };
        await using WebApplication boundary = OperatorBoundary.Create(new Uri(grafana.Urls.Single()), IPAddress.Loopback, "Remote-User", operators);
        await boundary.StartAsync();
        using HttpClient client = new() { BaseAddress = new Uri(boundary.Urls.Single()) };
        client.DefaultRequestHeaders.Add("Remote-User", "ordinary-registrant");
        client.DefaultRequestHeaders.Add("X-WEBAUTH-USER", "admin");
        client.DefaultRequestHeaders.Add("X-WEBAUTH-ROLE", "Admin");
        client.DefaultRequestHeaders.Add("Cookie", "session=forged");
        using HttpResponseMessage denied = await client.GetAsync("/api/user");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        client.DefaultRequestHeaders.Remove("Remote-User");
        client.DefaultRequestHeaders.Add("Remote-User", "verified-operator");
        string result = await client.GetStringAsync("/api/user");
        Assert.Contains("observability-viewer", result, StringComparison.Ordinal);
        Assert.DoesNotContain("admin", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("forged", result, StringComparison.Ordinal);
        await using WebApplication wrongPeer = OperatorBoundary.Create(new Uri(grafana.Urls.Single()), IPAddress.Parse("192.0.2.4"), "Remote-User", operators);
        await wrongPeer.StartAsync();
        using HttpResponseMessage forgedPeer = await client.GetAsync(wrongPeer.Urls.Single() + "/api/user");
        Assert.Equal(HttpStatusCode.Forbidden, forgedPeer.StatusCode);
    }
}
