using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using Wallow.ServiceDefaults;

namespace Wallow.Api.Tests;

public sealed class IndependentTelemetryTests
{
    [Fact]
    public async Task HttpFailure_ExportsCorrelatedSanitizedSignals_WithoutFollowingCollectorRedirects()
    {
        List<JsonElement> received = [];
        WebApplicationBuilder receiverBuilder = WebApplication.CreateSlimBuilder();
        receiverBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using WebApplication receiver = receiverBuilder.Build();
        receiver.MapPost("/v1/{signal}", async (HttpContext context) =>
        {
            context.Request.Headers.Authorization.ToString().Should().Be("Bearer test.credential");
            using JsonDocument document = await JsonDocument.ParseAsync(context.Request.Body);
            if (received.Count < 100) { received.Add(document.RootElement.Clone()); }
            return Results.Ok();
        });
        await receiver.StartAsync();
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telemetry:Export:Endpoint"] = receiver.Urls.Single(),
            ["Telemetry:Export:Credential"] = "test.credential",
            ["Telemetry:Export:Environment"] = "test",
            ["Telemetry:Export:Release"] = "1.0.0",
        });
        builder.AddServiceDefaults();
        await using WebApplication application = builder.Build();
        IndependentTelemetry telemetry = application.Services.GetRequiredService<IndependentTelemetry>();
        application.MapGet("/failure", () =>
        {
            InvalidOperationException error = new("PRIVATE-EXCEPTION");
            Dictionary<string, object?> attributes = new()
            {
                ["email"] = "PRIVATE-EMAIL@example.com",
                ["nested"] = new Dictionary<string, object?> { ["token"] = "PRIVATE-TOKEN" },
                ["url"] = "https://example.com/path?secret=PRIVATE-QUERY",
                ["detail"] = "Request failed at https://user:PRIVATE-CREDENTIAL@example.com/path?secret=PRIVATE-EMBEDDED",
                ["header"] = "Basic PRIVATE-BASIC",
                ["attempt"] = 2,
                ["local"] = "http://[::1]/path",
                ["nestedLocal"] = new Dictionary<string, object?> { ["value"] = "::1" },
            };
            telemetry.WriteLog(LogLevel.Error, "operation.failed", attributes, error);
            telemetry.WriteLog(LogLevel.Error, "operation.failed", attributes, error);
            return Results.StatusCode(500);
        });
        await application.StartAsync();
        using HttpClient caller = new();
        caller.DefaultRequestHeaders.Add("traceparent", "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01");
        using IDisposable suppression = SuppressInstrumentationScope.Begin();
        using HttpResponseMessage result = await caller.GetAsync(application.Urls.Single() + "/failure");
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        await telemetry.FlushAsync(CancellationToken.None);
        string wire = JsonSerializer.Serialize(received);
        wire.Contains("0123456789abcdef0123456789abcdef", StringComparison.Ordinal).Should().BeTrue("the incoming trace is preserved");
        wire.Should().Contain("operation.failed").And.Contain("resourceSpans");
        wire.Should().Contain("wallow_request_duration_milliseconds").And.NotContain("PRIVATE-").And.NotContain("::1");
        received.SelectMany(value => value.TryGetProperty("resourceLogs", out JsonElement logs)
            ? logs.EnumerateArray().SelectMany(resource => resource.GetProperty("scopeLogs").EnumerateArray())
                .SelectMany(scope => scope.GetProperty("logRecords").EnumerateArray())
            : [])
            .Should().ContainSingle();
        telemetry.Stats().Failed.Should().Be(0);
        telemetry.Stats().QueuedBytes.Should().Be(0);
    }

    [Fact]
    public async Task OutageAndOverflow_AreBoundedAndCounted_WithoutThrowingIntoApplication()
    {
        using IndependentTelemetry telemetry = new(new Uri("http://127.0.0.1:1"), "test.credential", "test", "proof");
        for (int index = 0; index < 20000; index++)
        {
            telemetry.WriteLog(LogLevel.Information, "buffer.fill", new Dictionary<string, object?> { ["data"] = new string('x', 900) });
        }
        telemetry.Stats().QueuedBytes.Should().BeLessThanOrEqualTo(8 * 1024 * 1024);
        telemetry.Stats().Dropped.Should().BePositive();
        Stopwatch elapsed = Stopwatch.StartNew();
        await telemetry.FlushAsync(CancellationToken.None);
        elapsed.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(6));
        telemetry.Stats().Failed.Should().BePositive();
        telemetry.Stats().QueuedBytes.Should().Be(0);
    }
    [Fact]
    public async Task ShutdownDuringAnExport_DrainsTheRemainingQueueWithinItsBudget()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        List<string> bodies = [];
        WebApplicationBuilder receiverBuilder = WebApplication.CreateSlimBuilder();
        receiverBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using WebApplication receiver = receiverBuilder.Build();
        receiver.MapPost("/v1/logs", async (HttpContext context) =>
        {
            using StreamReader reader = new(context.Request.Body);
            bodies.Add(await reader.ReadToEndAsync(context.RequestAborted));
            started.TrySetResult();
            await release.Task;
            return Results.Ok();
        });
        await receiver.StartAsync();
        using IndependentTelemetry telemetry = new(new Uri(receiver.Urls.Single()), "test.credential", "test", "proof");
        telemetry.WriteLog(LogLevel.Information, "before.shutdown", new Dictionary<string, object?>());
        await telemetry.StartAsync(CancellationToken.None);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(7));
            telemetry.WriteLog(LogLevel.Information, "during.shutdown", new Dictionary<string, object?>());
            Task shutdown = telemetry.StopAsync(CancellationToken.None);
            release.TrySetResult();
            await shutdown.WaitAsync(TimeSpan.FromSeconds(6));
            string.Join("", bodies).Should().Contain("during.shutdown");
            telemetry.Stats().QueuedBytes.Should().Be(0);
            telemetry.Stats().Dropped.Should().Be(0);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public void InvalidAttributeEnumeration_DoesNotEscapeIntoApplicationCode()
    {
        using IndependentTelemetry telemetry = new(new Uri("http://127.0.0.1:1"), "test.credential", "test", "proof");
        Action capture = () => telemetry.WriteLog(LogLevel.Information, "capture.failure", new Dictionary<string, object?> { ["data"] = new InvalidSequence() });
        capture.Should().NotThrow();
        telemetry.Stats().Dropped.Should().Be(1);
    }

    private sealed class InvalidSequence : System.Collections.IEnumerable
    {
        public System.Collections.IEnumerator GetEnumerator() => throw new InvalidOperationException("Invalid application value");
    }

}
