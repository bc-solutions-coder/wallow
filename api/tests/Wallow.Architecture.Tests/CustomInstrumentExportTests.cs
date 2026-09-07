using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Wallow.Identity.Application.Telemetry;
using Wallow.Notifications.Application.Channels.Email.Telemetry;
using Wallow.ServiceDefaults;
using Wallow.Shared.Kernel;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Checks collection of selected metric names and listener activity for selected source names.
/// Includes bare and dotted prefixes, a custom prefix and unrelated-name controls.
/// </summary>
public class CustomInstrumentExportTests
{
    private const string PrefixConfigKey = "Logging:NamespacePrefix";

    /// <summary>
    /// Alternate prefix used to detect hardcoded registration names.
    /// </summary>
    private const string ForkPrefix = "Contoso";

    private static readonly string _observabilityDocsPath = Path.Combine(
        FindRepoRoot(),
        "docs",
        "operations",
        "observability.md");



    [Fact]
    public void Probe_ShouldObserveAMeter_ThatIsExplicitlyRegistered()
    {
        bool collected = IsMeterCollected("Wallow.Tests.MeterProbeControl", alsoRegister: "Wallow.Tests.MeterProbeControl");

        collected.Should().BeTrue(
            "this is the harness control: a meter the test itself passes to AddMeter must reach the " +
            "metric reader. If this fails the probe is broken and every other metric assertion in " +
            "this file is meaningless, independently of the registration under test");
    }

    [Theory]
    [InlineData("Wallow")]
    [InlineData("Wallow.Messaging")]
    [InlineData("Wallow.Cache")]
    [InlineData("Wallow.Identity")]
    [InlineData("Wallow.Health")]
    public void AddServiceDefaults_ShouldExport_EveryCustomMeter(string meterName)
    {
        bool collected = IsMeterCollected(meterName);

        collected.Should().BeTrue(
            "ConfigureOpenTelemetry must AddMeter(\"{0}\") — without it the SDK never subscribes to " +
            "that meter, so its instruments are recorded in-process and never exported",
            meterName);
    }

    [Theory]
    [InlineData("Contoso")]
    [InlineData("Contoso.Messaging")]
    [InlineData("Contoso.Cache")]
    [InlineData("Contoso.Identity")]
    [InlineData("Contoso.Health")]
    public void AddServiceDefaults_ShouldExport_CustomMeters_UnderAConfiguredNamespacePrefix(string meterName)
    {
        bool collected = IsMeterCollected(meterName, namespacePrefix: ForkPrefix);

        collected.Should().BeTrue(
            "a fork setting {0}={1} gets its meters named \"{2}\" via Diagnostics.Initialize, so the " +
            "registration must read the prefix from builder.Configuration — AddServiceDefaults runs " +
            "before Diagnostics.Initialize, so Diagnostics state is not readable at that point",
            PrefixConfigKey,
            ForkPrefix,
            meterName);
    }

    [Fact]
    public void AddServiceDefaults_ShouldNotExport_MetersOutsideTheConfiguredPrefix()
    {
        bool collected = IsMeterCollected("Zzz.Unrelated.ThirdParty");

        collected.Should().BeFalse(
            "registration must stay scoped to our own prefix — a blanket AddMeter(\"*\") would " +
            "subscribe to every third-party meter in the process and flood the collector");
    }

    [Theory]
    [InlineData("Wolverine:Wallow.Api")]
    [InlineData("Wolverine:Contoso.Api")]
    public void AddServiceDefaults_ShouldExport_TheWolverineRuntimeMeter(string meterName)
    {
        bool collected = IsMeterCollected(meterName);

        collected.Should().BeTrue(
            "Wolverine records its built-in instruments (wolverine-dead-letter-queue, " +
            "wolverine-inbox-count, …) on a meter named \"Wolverine:\" + ServiceName, which " +
            "defaults to the application assembly name — so the registration must be the " +
            "wildcard pattern \"Wolverine:*\", or \"{0}\" is recorded in-process and thrown " +
            "away and a dead-letter pile-up stays invisible (Wallow-qi90.2)",
            meterName);
    }



    [Fact]
    public void Probe_ShouldObserveAnActivitySource_ThatIsExplicitlyRegistered()
    {
        bool listened = IsActivitySourceListenedTo(
            "Wallow.Tests.SourceProbeControl",
            alsoRegister: "Wallow.Tests.SourceProbeControl");

        listened.Should().BeTrue(
            "this is the harness control: a source the test itself passes to AddSource must produce " +
            "a non-null Activity. If this fails the probe is broken and every other trace assertion " +
            "in this file is meaningless, independently of the registration under test");
    }

    [Theory]
    [InlineData("Wallow")]
    [InlineData("Wallow.Identity")]
    [InlineData("Wallow.Notifications.Email")]
    public void AddServiceDefaults_ShouldExport_EveryCustomActivitySource(string sourceName)
    {
        bool listened = IsActivitySourceListenedTo(sourceName);

        listened.Should().BeTrue(
            "ConfigureOpenTelemetry must AddSource(\"{0}\") — without a listener StartActivity " +
            "returns null and the span is never created, let alone exported",
            sourceName);
    }

    [Theory]
    [InlineData("Contoso")]
    [InlineData("Contoso.Identity")]
    [InlineData("Contoso.Notifications.Email")]
    public void AddServiceDefaults_ShouldExport_CustomActivitySources_UnderAConfiguredNamespacePrefix(
        string sourceName)
    {
        bool listened = IsActivitySourceListenedTo(sourceName, namespacePrefix: ForkPrefix);

        listened.Should().BeTrue(
            "a fork setting {0}={1} gets its activity sources named \"{2}\", so the registration " +
            "must derive the names from configuration rather than hard-coding \"Wallow\"",
            PrefixConfigKey,
            ForkPrefix,
            sourceName);
    }

    [Fact]
    public void AddServiceDefaults_ShouldNotExport_ActivitySourcesOutsideTheConfiguredPrefix()
    {
        bool listened = IsActivitySourceListenedTo("Zzz.Unrelated.ThirdParty");

        listened.Should().BeFalse(
            "registration must stay scoped to our own prefix — a blanket AddSource(\"*\") would " +
            "listen to every third-party activity source in the process");
    }



    [Fact]
    public void CustomInstrumentNames_ShouldMatch_TheNamesTheseTestsAssert()
    {
        // Tie the probe names to the production telemetry names.
        Diagnostics.Meter.Name.Should().Be("Wallow");
        Diagnostics.ActivitySource.Name.Should().Be("Wallow");
        using Meter messagingMeter = Diagnostics.CreateMeter("Messaging");
        using Meter cacheMeter = Diagnostics.CreateMeter("Cache");
        using Meter identityMeter = Diagnostics.CreateMeter("Identity");
        using Meter healthMeter = Diagnostics.CreateMeter("Health");

        messagingMeter.Name.Should().Be("Wallow.Messaging");
        cacheMeter.Name.Should().Be("Wallow.Cache");
        identityMeter.Name.Should().Be("Wallow.Identity");
        healthMeter.Name.Should().Be("Wallow.Health");
        IdentityModuleTelemetry.ActivitySource.Name.Should().Be("Wallow.Identity");
        EmailModuleTelemetry.ActivitySource.Name.Should().Be("Wallow.Notifications.Email");
    }



    [Theory]
    [InlineData("no meters or activity sources")]
    [InlineData("never exported")]
    [InlineData("until the meters are registered")]
    public void ObservabilityDocs_ShouldNotClaim_ThatCustomInstrumentsAreNeverExported(string staleClaim)
    {
        string source = File.ReadAllText(_observabilityDocsPath);

        source.Should().NotContain(
            staleClaim,
            "the 'Exporting Custom Instruments' section of docs/operations/observability.md " +
            "documents the gap this bead closes; once ConfigureOpenTelemetry registers the meters " +
            "and sources it must describe what is registered, not tell the reader to add it");
    }

    [Fact]
    public void ObservabilityDocs_ShouldList_TheHealthCheckGauge_InTheCustomInstrumentTable()
    {
        string source = File.ReadAllText(_observabilityDocsPath);

        source.Should().Contain(
            "wallow.healthcheck.status",
            "the custom instrument table omits the Health gauge recorded by " +
            "HealthCheckMetricsPublisher, so a reader auditing which meters need registering would " +
            "miss Wallow.Health entirely");
    }

    [Fact]
    public void ObservabilityDocs_ShouldList_TheDeadLetterDepthGauge_InTheCustomInstrumentTable()
    {
        string source = File.ReadAllText(_observabilityDocsPath);

        source.Should().Contain(
            "wallow.messaging.dead_letter_queue.depth",
            "the custom instrument table is the census a reader audits against ServiceDefaults " +
            "registration, and the DLQ depth gauge recorded by WolverineDeadLetterQueueHealthCheck " +
            "is part of it (Wallow-qi90.2)");
    }

    [Fact]
    public void ObservabilityDocs_ShouldDocument_TheWolverineRuntimeMeterExport()
    {
        string source = File.ReadAllText(_observabilityDocsPath);

        source.Should().Contain(
            "Wolverine:*",
            "ConfigureOpenTelemetry registers Wolverine's runtime meter by this wildcard, and " +
            "the docs must say so or a reader auditing exported meters would conclude Wolverine " +
            "metrics are still dropped");

        source.Should().Contain(
            "wolverine-dead-letter-queue",
            "the built-in dead-letter counter is the alerting signal Wallow-qi90.2 exists to " +
            "surface — documenting the meter without naming it buries the lede");
    }



    /// <summary>
    /// Records a probe counter and checks whether the configured provider exports it to the test reader.
    /// </summary>
    private static bool IsMeterCollected(
        string probeMeterName,
        string? namespacePrefix = null,
        string? alsoRegister = null)
    {
        List<string> exportedMeterNames = [];

        WebApplicationBuilder builder = CreateBuilder(namespacePrefix);

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                if (alsoRegister is not null)
                {
                    metrics.AddMeter(alsoRegister);
                }

                metrics.AddReader(
                    new BaseExportingMetricReader(new CollectingMetricExporter(exportedMeterNames)));
            });

        WebApplication app = builder.Build();
        try
        {
            MeterProvider provider = app.Services.GetRequiredService<MeterProvider>();

            using Meter probeMeter = new(probeMeterName);
            Counter<long> probeCounter = probeMeter.CreateCounter<long>("wallow.tests.instrument_probe");
            probeCounter.Add(1);

            provider.ForceFlush(10_000);

            return exportedMeterNames.Contains(probeMeterName, StringComparer.Ordinal);
        }
        finally
        {
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Builds tracing and reports whether the probe source creates an activity.
    /// </summary>
    private static bool IsActivitySourceListenedTo(
        string probeSourceName,
        string? namespacePrefix = null,
        string? alsoRegister = null)
    {
        WebApplicationBuilder builder = CreateBuilder(namespacePrefix);

        if (alsoRegister is not null)
        {
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing => tracing.AddSource(alsoRegister));
        }

        WebApplication app = builder.Build();
        Activity? previous = Activity.Current;
        try
        {
            _ = app.Services.GetRequiredService<TracerProvider>();

            // Remove the ambient parent so the probe uses root sampling.
            Activity.Current = null;

            using ActivitySource probeSource = new(probeSourceName);
            using Activity? activity = probeSource.StartActivity("wallow.tests.source_probe");

            return activity is not null;
        }
        finally
        {
            Activity.Current = previous;
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static WebApplicationBuilder CreateBuilder(string? namespacePrefix)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Disable the remote exporter for this in-process probe.
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;
        builder.Configuration[PrefixConfigKey] = namespacePrefix;

        builder.AddServiceDefaults();

        return builder;
    }

    private static string FindRepoRoot()
    {
        string? directory = Directory.GetCurrentDirectory();

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "pnpm-workspace.yaml")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException(
            "Could not locate the repository root (no pnpm-workspace.yaml found walking up from " +
            Directory.GetCurrentDirectory());
    }

    /// <summary>Captures the meter name of every metric the reader hands over.</summary>
    private sealed class CollectingMetricExporter : BaseExporter<Metric>
    {
        private readonly List<string> _meterNames;

        public CollectingMetricExporter(List<string> meterNames)
        {
            _meterNames = meterNames;
        }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (Metric metric in batch)
            {
                _meterNames.Add(metric.MeterName);
            }

            return ExportResult.Success;
        }
    }
}
