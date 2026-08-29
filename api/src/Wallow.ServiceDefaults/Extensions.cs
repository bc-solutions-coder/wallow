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

    /// <summary>
    /// Configuration key holding the telemetry namespace prefix a fork runs under. The same key
    /// feeds <c>Diagnostics.Initialize</c>, which names every meter and activity source.
    /// </summary>
    private const string NamespacePrefixKey = "Logging:NamespacePrefix";

    /// <summary>
    /// Prefix <c>Diagnostics</c> uses when a fork configures none.
    /// </summary>
    private const string DefaultNamespacePrefix = "Wallow";

    /// <summary>
    /// Wolverine names its runtime meter "Wolverine:" + ServiceName, and ServiceName defaults to
    /// the application assembly name, so this is a wildcard rather than a literal: it must keep
    /// matching when a fork renames the API assembly. That meter carries the built-in messaging
    /// instruments, including the dead-letter counter this repo alerts on (Wallow-qi90.2).
    /// </summary>
    private const string WolverineMeterPattern = "Wolverine:*";

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
        // Kept out of the API description: an infrastructure liveness probe has no place in the
        // public v1 document, where its untyped 200 would generate an SDK client method returning
        // unknown.
        // AllowAnonymous is required: a host with an authenticated FallbackPolicy would
        // otherwise challenge the orchestrator's unauthenticated liveness probe.
        app.MapGet("/alive", () => Results.Ok("Alive")).AllowAnonymous().ExcludeFromDescription();

        return app;
    }

    private static void ConfigureOpenTelemetry(IHostApplicationBuilder builder)
    {
        double samplingRatio = ResolveTraceSamplingRatio(builder.Configuration);

        // The SDK only collects from meters and activity sources it has been told about, so the
        // instruments Diagnostics creates have to be registered here or they are recorded in-process
        // and thrown away. AddServiceDefaults runs before Diagnostics.Initialize, so the prefix comes
        // from configuration rather than from Diagnostics state.
        string namespacePrefix = builder.Configuration[NamespacePrefixKey] ?? DefaultNamespacePrefix;

        // The wildcard is a suffix match, so it covers every module-scoped name
        // (Wallow.Messaging, Wallow.Identity, …) but not the bare prefix itself.
        string moduleNamespaces = $"{namespacePrefix}.*";

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(namespacePrefix, moduleNamespaces);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddProcessInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(namespacePrefix, moduleNamespaces, WolverineMeterPattern);
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
