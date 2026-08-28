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
/// Guards that every custom OpenTelemetry instrument in this repo is actually exported (bead
/// Wallow-t955). Before this bead <c>ConfigureOpenTelemetry</c> contained zero <c>AddMeter</c> and
/// zero <c>AddSource</c> calls, so the SDK collected nothing from our own <c>Meter</c> /
/// <c>ActivitySource</c> instances — the counters and histograms were recorded in-process and thrown
/// away, and only the built-in ASP.NET Core / HttpClient / process / runtime instrumentation ever
/// reached the exporter.
///
/// The contract these tests pin down:
/// <list type="bullet">
/// <item>Every meter created through <see cref="Diagnostics.CreateMeter" /> that carries an
/// instrument — <c>Messaging</c>, <c>Cache</c>, <c>Identity</c>, <c>Health</c> — is collected, as is
/// the bare <see cref="Diagnostics.Meter" /> a fork may record on. The bare name matters
/// independently because <c>AddMeter</c>'s <c>*</c> wildcard is a suffix match:
/// <c>AddMeter("Wallow.*")</c> does NOT match a meter named exactly <c>"Wallow"</c>.</item>
/// <item>Both live activity sources — <c>Wallow.Identity</c> and
/// <c>Wallow.Notifications.Email</c> — plus the bare <see cref="Diagnostics.ActivitySource" /> are
/// listened to.</item>
/// <item>The prefix is runtime-configurable. <c>Program.cs</c> calls
/// <c>Diagnostics.Initialize(configuration["Logging:NamespacePrefix"])</c>, so a fork running under
/// <c>"Contoso"</c> must get <c>Contoso.Messaging</c> and friends registered. Critically,
/// <c>AddServiceDefaults</c> runs BEFORE <c>Diagnostics.Initialize</c>, so the registration has to
/// read the prefix from <c>builder.Configuration</c> rather than from <see cref="Diagnostics" />
/// static state.</item>
/// <item>Registration stays scoped to our prefix — a blanket <c>AddMeter("*")</c> would drag in
/// every third-party meter in the process and is not an acceptable fix.</item>
/// </list>
///
/// Rather than reflecting over SDK internals, these tests probe the built providers functionally: a
/// meter is "exported" if a counter recorded on it reaches a metric reader attached to the
/// <see cref="MeterProvider" /> <c>AddServiceDefaults</c> configured, and an activity source is
/// "listened to" if <see cref="ActivitySource.StartActivity(string, ActivityKind)" /> returns a
/// non-null activity (with no listener the SDK short-circuits and returns null). The two
/// <c>Probe_Should…</c> control tests register a name inline and assert the probe sees it, so a
/// failure in the tests below is a missing registration rather than a broken harness.
/// </summary>
public class CustomInstrumentExportTests
{
    private const string PrefixConfigKey = "Logging:NamespacePrefix";

    /// <summary>Prefix a fork might configure, used to prove the registration is not hard-coded.</summary>
    private const string ForkPrefix = "Contoso";

    private static readonly string _observabilityDocsPath = Path.Combine(
        FindRepoRoot(),
        "docs",
        "operations",
        "observability.md");

    // ---- metrics -------------------------------------------------------------------------

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

    // ---- traces --------------------------------------------------------------------------

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

    // ---- drift guard ---------------------------------------------------------------------

    [Fact]
    public void CustomInstrumentNames_ShouldMatch_TheNamesTheseTestsAssert()
    {
        // If someone renames a module telemetry holder or the Diagnostics prefix scheme, the
        // literals above would silently stop covering the real instruments. Bind them to the live
        // API here so that drift fails loudly instead of quietly disabling the export tests.
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

    // ---- documentation -------------------------------------------------------------------

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

    // ---- helpers -------------------------------------------------------------------------

    /// <summary>
    /// Records a counter on a meter named <paramref name="probeMeterName" /> and reports whether it
    /// reached a reader attached to the <see cref="MeterProvider" /> that <c>AddServiceDefaults</c>
    /// configured. A meter the SDK was never told about is dropped at publish time, so this is a
    /// direct functional test of <c>AddMeter</c>.
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
    /// Reports whether the <see cref="TracerProvider" /> that <c>AddServiceDefaults</c> configured
    /// listens to <paramref name="probeSourceName" />. <see cref="ActivitySource.StartActivity(string, ActivityKind)" />
    /// returns null when no listener has opted into the source, so a non-null activity is proof the
    /// source was registered with <c>AddSource</c>.
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

            // ParentBased sampling defers to an ambient parent; start from a clean slate so the
            // result reflects the root sampler and the listener, not whatever xunit left behind.
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

        // Keep the providers in-process: an OTLP endpoint is irrelevant to which meters and sources
        // the SDK subscribes to.
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
