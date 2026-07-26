using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Wallow.ServiceDefaults;

namespace Wallow.Architecture.Tests;

/// <summary>
/// Guards the trace sampler configuration (bead Wallow-mbe0). Before this bead
/// <c>ConfigureOpenTelemetry</c> set no sampler at all, so the OpenTelemetry SDK fell back to
/// <c>ParentBased{AlwaysOnSampler}</c> and exported 100% of traces — tolerable locally, but it
/// floods the collector and inflates cost/storage in production.
///
/// The contract these tests pin down:
/// <list type="bullet">
/// <item><c>AddServiceDefaults</c> installs <c>ParentBased(TraceIdRatioBased(ratio))</c>.
/// Parent-based is required so a trace already sampled upstream stays sampled through our
/// services.</item>
/// <item>The ratio is bound from the configuration key
/// <c>OpenTelemetry:TraceSamplingRatio</c> (the existing <c>OpenTelemetry</c> section in
/// <c>api/src/Wallow.Api/appsettings.json</c>, so a container overrides it with
/// <c>OpenTelemetry__TraceSamplingRatio</c>), defaulting to <c>1.0</c> so local development keeps
/// full-fidelity traces.</item>
/// <item>An out-of-range or non-numeric value must NOT brick host startup — a fork typo in an env
/// var has to degrade to a usable ratio, not an unhandled
/// <c>ArgumentOutOfRangeException</c> out of <c>TraceIdRatioBasedSampler</c>.</item>
/// </list>
///
/// The sampler is read back off the built <c>TracerProviderSdk</c> through its internal
/// <c>Sampler</c> property and asserted via the public <c>Sampler.Description</c>, which OpenTelemetry
/// formats as <c>ParentBased{TraceIdRatioBasedSampler{0.100000}}</c>. That is the only seam the SDK
/// offers for observing a configured sampler, and it is stable across the 1.x line.
///
/// The compose/appsettings/docs assertions use the same static source-inspection pattern as
/// <see cref="CiAuthImageBuildTests" /> because a deployed sampling rate cannot be observed from a
/// unit test.
/// </summary>
public class TraceSamplerConfigurationTests
{
    private const string RatioConfigKey = "OpenTelemetry:TraceSamplingRatio";

    private const string RatioEnvKey = "OpenTelemetry__TraceSamplingRatio";

    private static readonly string _repoRoot = FindRepoRoot();

    private static readonly string _productionComposePath = Path.Combine(
        _repoRoot,
        "docker",
        "docker-compose.production.yml");

    private static readonly string _apiAppSettingsPath = Path.Combine(
        _repoRoot,
        "api",
        "src",
        "Wallow.Api",
        "appsettings.json");

    private static readonly string _observabilityDocsPath = Path.Combine(
        _repoRoot,
        "docs",
        "operations",
        "observability.md");

    // ---- sampler wiring ------------------------------------------------------------------

    [Fact]
    public void AddServiceDefaults_ShouldConfigureParentBasedRatioSampler_DefaultingToFullSampling()
    {
        Sampler sampler = BuildSampler(configuredRatio: null);

        sampler.Should().BeOfType<ParentBasedSampler>(
            "a sampled upstream trace must stay sampled through our services, so the root sampler " +
            "has to be wrapped in ParentBased");

        sampler.Description.Should().Be(
            "ParentBased{TraceIdRatioBasedSampler{1.000000}}",
            "with no {0} configured the ratio must default to 1.0 so local development keeps " +
            "full-fidelity traces — and the inner sampler must be TraceIdRatioBased, not the " +
            "SDK's AlwaysOn fallback",
            RatioConfigKey);
    }

    [Theory]
    [InlineData("0.1", "0.100000")]
    [InlineData("0.25", "0.250000")]
    [InlineData("1", "1.000000")]
    [InlineData("0", "0.000000")]
    public void AddServiceDefaults_ShouldBindTraceSamplingRatio_FromConfiguration(
        string configuredRatio,
        string expectedRatio)
    {
        Sampler sampler = BuildSampler(configuredRatio);

        sampler.Description.Should().Be(
            $"ParentBased{{TraceIdRatioBasedSampler{{{expectedRatio}}}}}",
            "the sampling ratio must be bound from {0} so production can run far below 1.0 while " +
            "local development stays at full fidelity",
            RatioConfigKey);
    }

    [Theory]
    [InlineData("5", "1.000000")]
    [InlineData("50", "1.000000")]
    [InlineData("-0.5", "0.000000")]
    public void AddServiceDefaults_ShouldClampTraceSamplingRatio_WhenConfiguredOutOfRange(
        string configuredRatio,
        string expectedRatio)
    {
        Sampler sampler = BuildSampler(configuredRatio);

        sampler.Description.Should().Be(
            $"ParentBased{{TraceIdRatioBasedSampler{{{expectedRatio}}}}}",
            "an operator writing '{0}' into {1} (e.g. meaning a percentage) must be clamped into " +
            "[0,1] — TraceIdRatioBasedSampler throws ArgumentOutOfRangeException outside that range, " +
            "which would take down host startup",
            configuredRatio,
            RatioConfigKey);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("0.1.2")]
    public void AddServiceDefaults_ShouldFallBackToFullSampling_WhenTraceSamplingRatioIsNotANumber(
        string configuredRatio)
    {
        Sampler sampler = BuildSampler(configuredRatio);

        sampler.Description.Should().Be(
            "ParentBased{TraceIdRatioBasedSampler{1.000000}}",
            "an unparseable {0} must fall back to the 1.0 default rather than throwing out of " +
            "configuration binding during startup",
            RatioConfigKey);
    }

    // ---- deployed configuration --------------------------------------------------------

    [Fact]
    public void ProductionCompose_ShouldConfigure_TraceSamplingRatioBelowFullSampling()
    {
        string source = File.ReadAllText(_productionComposePath);

        source.Should().Contain(
            RatioEnvKey,
            "docker-compose.production.yml must set {0} on the wallow-api service — leaving the " +
            "1.0 default in production exports every trace",
            RatioEnvKey);

        Match match = Regex.Match(
            source,
            RatioEnvKey + @":\s*""?(?:\$\{[A-Za-z0-9_]+:-)?(?<ratio>-?[0-9]*\.?[0-9]+)");

        match.Success.Should().BeTrue(
            "the {0} entry in docker-compose.production.yml must carry a numeric ratio, either " +
            "literally or as the default of a ${{VAR:-ratio}} substitution",
            RatioEnvKey);

        double ratio = double.Parse(
            match.Groups["ratio"].Value,
            CultureInfo.InvariantCulture);

        ratio.Should().BeInRange(
            0d,
            0.99d,
            "the production sampling ratio must be meaningfully below 1.0 so the collector is not " +
            "flooded, and within the [0,1] range TraceIdRatioBasedSampler accepts");
    }

    [Fact]
    public void ApiAppSettings_ShouldDeclare_TraceSamplingRatio_AtFullSamplingForLocalDev()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(_apiAppSettingsPath));

        JsonElement openTelemetrySection;
        document.RootElement.TryGetProperty("OpenTelemetry", out openTelemetrySection)
            .Should().BeTrue("appsettings.json already carries an OpenTelemetry section");

        JsonElement ratioElement;
        openTelemetrySection.TryGetProperty("TraceSamplingRatio", out ratioElement)
            .Should().BeTrue(
                "the OpenTelemetry section must declare TraceSamplingRatio so the knob is " +
                "discoverable to forks rather than living only as a code default");

        ratioElement.GetDouble().Should().Be(
            1d,
            "the checked-in appsettings.json is the local-development baseline and must keep full " +
            "sampling; production lowers it via {0}",
            RatioEnvKey);
    }

    // ---- documentation ------------------------------------------------------------------

    [Theory]
    [InlineData("none is configured")]
    [InlineData("no sampler is configured")]
    [InlineData("no ratio-based sampler")]
    public void ObservabilityDocs_ShouldNotClaim_ThatNoSamplerIsConfigured(string staleClaim)
    {
        string source = File.ReadAllText(_observabilityDocsPath);

        source.Should().NotContain(
            staleClaim,
            "docs/operations/observability.md still describes the pre-sampler behaviour; both the " +
            "Sampling bullet and the Performance Considerations section must describe the " +
            "ParentBased/TraceIdRatioBased sampler that is now wired up");
    }

    [Theory]
    [InlineData("ParentBased")]
    [InlineData("TraceIdRatioBased")]
    [InlineData(RatioConfigKey)]
    public void ObservabilityDocs_ShouldDocument_TheConfiguredSampler(string expectedMention)
    {
        string source = File.ReadAllText(_observabilityDocsPath);

        source.Should().Contain(
            expectedMention,
            "docs/operations/observability.md must document the sampler shape and the " +
            "configuration key operators use to tune it");
    }

    // ---- helpers ------------------------------------------------------------------------

    /// <summary>
    /// Builds a host through <c>AddServiceDefaults</c> with the given
    /// <c>OpenTelemetry:TraceSamplingRatio</c> value and returns the sampler the resulting
    /// <c>TracerProvider</c> was constructed with.
    /// </summary>
    private static Sampler BuildSampler(string? configuredRatio)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Keep the provider in-process: an OTLP endpoint is irrelevant to sampler selection.
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = null;
        builder.Configuration[RatioConfigKey] = configuredRatio;

        builder.AddServiceDefaults();

        WebApplication app = builder.Build();
        try
        {
            TracerProvider provider = app.Services.GetRequiredService<TracerProvider>();

            PropertyInfo? samplerProperty = provider.GetType().GetProperty(
                "Sampler",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            samplerProperty.Should().NotBeNull(
                "TracerProviderSdk exposes the configured sampler through an internal Sampler " +
                "property — if this is null the OpenTelemetry SDK changed shape and this test " +
                "needs updating, independently of the sampler wiring under test");

            Sampler? sampler = samplerProperty!.GetValue(provider) as Sampler;

            sampler.Should().NotBeNull(
                "ConfigureOpenTelemetry must call SetSampler so the tracer provider does not fall " +
                "back to the SDK's always-on default");

            return sampler!;
        }
        finally
        {
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
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
}
