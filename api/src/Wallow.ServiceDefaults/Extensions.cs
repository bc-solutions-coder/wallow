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
    /// Default root-trace sampling ratio when configuration is absent or invalid.
    /// </summary>
    private const double DefaultTraceSamplingRatio = 1.0;

    /// <summary>
    /// Namespace key shared with application diagnostics initialization.
    /// </summary>
    private const string NamespacePrefixKey = "Logging:NamespacePrefix";

    /// <summary>
    /// Prefix <c>Diagnostics</c> uses when a fork configures none.
    /// </summary>
    private const string DefaultNamespacePrefix = "Wallow";

    /// <summary>
    /// Wildcard for Wolverine runtime meters across service names.
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
        // Keep orchestrator liveness anonymous and outside the generated SDK.
        app.MapGet("/alive", () => Results.Ok("Alive")).AllowAnonymous().ExcludeFromDescription();

        return app;
    }

    private static void ConfigureOpenTelemetry(IHostApplicationBuilder builder)
    {
        double samplingRatio = ResolveTraceSamplingRatio(builder.Configuration);

        // Read the prefix from configuration because diagnostics initialize after service defaults.
        string namespacePrefix = builder.Configuration[NamespacePrefixKey] ?? DefaultNamespacePrefix;

        // Register both the bare namespace and its module names.
        string moduleNamespaces = $"{namespacePrefix}.*";

        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
                    .AddAspNetCoreInstrumentation(options => options.Filter = context =>
                        !Uri.TryCreate(builder.Configuration["Telemetry:Export:Endpoint"], UriKind.Absolute, out Uri? collector)
                        || !collector.IsLoopback || context.Connection.LocalPort != collector.Port)
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

        string? independentEndpoint = builder.Configuration["Telemetry:Export:Endpoint"];
        if (!string.IsNullOrWhiteSpace(independentEndpoint))
        {
            builder.Services.AddSingleton(_ => new IndependentTelemetry(
                new Uri(independentEndpoint),
                builder.Configuration["Telemetry:Export:Credential"] ?? "",
                builder.Configuration["Telemetry:Export:Environment"] ?? "production",
                builder.Configuration["Telemetry:Export:Release"] ?? "unknown"));
            builder.Services.AddHostedService(services => services.GetRequiredService<IndependentTelemetry>());
            builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
                .SetSampler(new AlwaysOnSampler())
                .AddProcessor(services => new SimpleActivityExportProcessor(new IndependentTraceExporter(services.GetRequiredService<IndependentTelemetry>()))));
        }

        string? otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            builder.Services.AddOpenTelemetry()
                .UseOtlpExporter();
        }
    }

    /// <summary>
    /// Uses full sampling for blank, invalid, or NaN values; clamps numeric values to [0,1].
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
