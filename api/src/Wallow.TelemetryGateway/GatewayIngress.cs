using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Wallow.TelemetryGateway;

public static class GatewayIngress
{
    private static readonly JsonSerializerOptions _faroJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static WebApplication Create(string databasePath, Uri collector, TimeProvider clock, Uri? faroCollector = null, string? collectorSecret = null, GatewayHealth? health = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 1024 * 1024;
            options.Limits.MaxConcurrentConnections = 64;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddDbContext<RegistryDb>(options => options.UseSqlite($"Data Source={databasePath}"));
        builder.Services.AddSingleton(_ => IngestionLimits.Create());
        builder.Services.AddHttpClient("collector", client =>
        {
            client.BaseAddress = collector;
            client.Timeout = TimeSpan.FromSeconds(5);
            client.MaxResponseContentBufferSize = 64 * 1024;
        });
        WebApplication app = builder.Build();
        health?.Observe(app);
        foreach (string route in new[] { "/v1/logs", "/v1/traces", "/v1/metrics", "/faro" })
        {
            app.MapPost(route, async (HttpContext context, RegistryDb db, IHttpClientFactory clients, PartitionedRateLimiter<Guid> limits) =>
            {
                string authorization = context.Request.Headers.Authorization.ToString();
                if (!authorization.StartsWith("Bearer ", StringComparison.Ordinal) || authorization.Length > 512)
                {
                    return Results.Unauthorized();
                }

                string[] parts = authorization[7..].Split('.', 2);
                if (parts.Length != 2)
                {
                    return Results.Unauthorized();
                }

                CredentialEntry? credential = await db.Credentials.AsNoTracking().SingleOrDefaultAsync(
                    entry => entry.Id == parts[0], context.RequestAborted).ConfigureAwait(false);
                if (credential is null || credential.Revoked || credential.ExpiresAt <= clock.GetUtcNow().ToUnixTimeMilliseconds()
                    || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(credential.Verifier), SHA256.HashData(Encoding.UTF8.GetBytes(parts[1]))))
                {
                    return Results.Unauthorized();
                }

                RegistrationEntry registration = await db.Registrations.AsNoTracking().SingleAsync(
                    entry => entry.Id == credential.RegistrationId, context.RequestAborted).ConfigureAwait(false);
                DesiredRegistration desired = JsonSerializer.Deserialize<DesiredRegistration>(registration.Desired)!;
                string environment = context.Request.Headers["X-Wallow-Environment"].ToString();
                if (desired.State != RegistrationState.Enabled || !desired.Environments.Contains(environment, StringComparer.Ordinal))
                {
                    return Results.Unauthorized();
                }

                string? mediaType = context.Request.ContentType?.Split(';', 2)[0].Trim();
                if (context.Request.Headers.ContentEncoding.Count > 0 || mediaType is not ("application/json" or "application/x-protobuf")
                    || (route == "/faro" && mediaType != "application/json"))
                {
                    return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
                }

                using RateLimitLease allowance = limits.AttemptAcquire(registration.Id);
                if (!allowance.IsAcquired)
                {
                    context.Response.Headers.RetryAfter = "1";
                    return Results.StatusCode(StatusCodes.Status429TooManyRequests);
                }

                using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                budget.CancelAfter(TimeSpan.FromSeconds(5));
                using MemoryStream buffer = new();
                try
                {
                    await context.Request.Body.CopyToAsync(buffer, budget.Token).ConfigureAwait(false);
                }
                catch (BadHttpRequestException error)
                {
                    return Results.StatusCode(error.StatusCode);
                }

                catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
                {
                    return Results.StatusCode(StatusCodes.Status408RequestTimeout);
                }

                string release = context.Request.Headers["X-Wallow-Release"].ToString();
                if (release.Length == 0)
                {
                    release = "unknown";
                }

                if (release.Length > 128 || !release.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
                {
                    return Results.BadRequest();
                }

                byte[] payload = buffer.ToArray();
                if (route == "/faro")
                {
                    try
                    {
                        JsonNode? node = JsonNode.Parse(payload);
                        if (node is not JsonObject faro || (faro["meta"] is not null && faro["meta"] is not JsonObject))
                        {
                            return Results.BadRequest();
                        }

                        JsonObject metadata = faro["meta"] as JsonObject ?? new JsonObject();
                        metadata["app"] = new JsonObject
                        {
                            ["name"] = desired.BrowserService,
                            ["namespace"] = desired.ApplicationId,
                            ["environment"] = environment,
                            ["version"] = release,
                        };
                        if (faro["meta"] is null)
                        {
                            faro["meta"] = metadata;
                        }

                        // Faro parses timestamp offsets without decoding JSON escapes.
                        payload = JsonSerializer.SerializeToUtf8Bytes(faro, _faroJson);
                    }
                    catch (JsonException)
                    {
                        return Results.BadRequest();
                    }
                }

                Uri destination = route == "/faro" ? faroCollector ?? new Uri(collector, "/collect") : new Uri(collector, route);
                using HttpRequestMessage forwarded = new(HttpMethod.Post, destination);
                forwarded.Content = new ByteArrayContent(payload);
                forwarded.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
                if (collectorSecret is not null)
                {
                    if (route == "/faro")
                    {
                        forwarded.Headers.Add("X-API-Key", collectorSecret);
                    }
                    else
                    {
                        forwarded.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", collectorSecret);
                    }
                }

                forwarded.Headers.Add("X-Wallow-Registration", registration.Id.ToString());
                forwarded.Headers.Add("X-Wallow-Application", desired.ApplicationId);
                forwarded.Headers.Add("X-Wallow-Service", route == "/faro" ? desired.BrowserService : desired.ServerService);
                forwarded.Headers.Add("X-Wallow-Release", release);
                forwarded.Headers.Add("X-Wallow-Environment", environment);
                using HttpClient client = clients.CreateClient("collector");
                try
                {
                    using HttpResponseMessage response = await client.SendAsync(forwarded, budget.Token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
                    {
                        return Results.StatusCode((int)response.StatusCode);
                    }

                    byte[] acknowledgement = await response.Content.ReadAsByteArrayAsync(budget.Token).ConfigureAwait(false);
                    context.Response.StatusCode = (int)response.StatusCode;
                    return Results.Bytes(acknowledgement, response.Content.Headers.ContentType?.ToString());
                }
                catch (HttpRequestException)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }
                catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
                {
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }
            });
        }
        return app;
    }
}
