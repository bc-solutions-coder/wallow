using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Wallow.ServiceDefaults;

public static class Extensions
{
    /// <summary>
    /// Configuration key holding the head-based trace sampling ratio. Overridable in containers as
    /// <c>OpenTelemetry__TraceSamplingRatio</c>.
    /// </summary>
    private const string TraceSamplingRatioKey = "OpenTelemetry:TraceSamplingRatio";

    /// <summary>
    /// Full sampling. Keeps local development at full trace fidelity; deployments lower it.
    /// </summary>
    private const double DefaultTraceSamplingRatio = 1.0;

    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddServiceDiscovery();
            http.AddStandardResilienceHandler();
        });

        builder.Services.AddHealthChecks();

        ConfigureOpenTelemetry(builder);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Health checks are mapped by each app's Program.cs with custom response writers
        // and tag-based filtering. Only map the /alive liveness probe here.
        app.MapGet("/alive", () => Results.Ok("Alive"));

        return app;
    }

    private static void ConfigureOpenTelemetry(IHostApplicationBuilder builder)
    {
        double samplingRatio = ResolveTraceSamplingRatio(builder.Configuration);

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddProcessInstrumentation()
                    .AddRuntimeInstrumentation();
            });

        string? otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            builder.Services.AddOpenTelemetry()
                .UseOtlpExporter();
        }
    }

    /// <summary>
    /// Reads the trace sampling ratio from configuration, tolerating anything an operator might
    /// type. A non-numeric value degrades to full sampling and an out-of-range value is clamped into
    /// <c>[0,1]</c> — <see cref="TraceIdRatioBasedSampler" /> throws outside that range, and a typo
    /// in an environment variable must not take down host startup.
    /// </summary>
    private static double ResolveTraceSamplingRatio(IConfiguration configuration)
    {
        string? configuredRatio = configuration[TraceSamplingRatioKey];

        if (string.IsNullOrWhiteSpace(configuredRatio)
            || !double.TryParse(
                configuredRatio,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double ratio)
            || double.IsNaN(ratio))
        {
            return DefaultTraceSamplingRatio;
        }

        return Math.Clamp(ratio, 0d, 1d);
    }
}
