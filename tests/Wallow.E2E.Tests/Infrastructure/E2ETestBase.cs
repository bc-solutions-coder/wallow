using Microsoft.Playwright;
using Wallow.E2E.Tests.Fixtures;
using Xunit.Abstractions;

namespace Wallow.E2E.Tests.Infrastructure;

[Trait("Category", "E2E")]
public abstract class E2ETestBase : IClassFixture<DockerComposeFixture>, IClassFixture<PlaywrightFixture>, IAsyncLifetime
{
    private readonly ITestOutputHelper? _testOutputHelper;
    private bool _testFailed;

    protected IPage Page { get; private set; } = null!;
    protected IBrowserContext Context { get; private set; } = null!;
    protected DockerComposeFixture Docker { get; }
    protected PlaywrightFixture Playwright { get; }

    protected E2ETestBase(DockerComposeFixture docker, PlaywrightFixture playwright, ITestOutputHelper? testOutputHelper = null)
    {
        Docker = docker;
        Playwright = playwright;
        _testOutputHelper = testOutputHelper;
    }

    public virtual async Task InitializeAsync()
    {
        bool recordVideo = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("E2E_VIDEO"));

        Context = await Playwright.CreateBrowserContextAsync(recordVideo: recordVideo);

        bool enableTracing = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("E2E_TRACING"));
        if (enableTracing)
        {
            await Context.Tracing.StartAsync(new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true,
            });
        }

        Page = await Context.NewPageAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (_testFailed)
        {
            await SaveFailureArtifactsAsync();
        }

        await Page.CloseAsync();
        await Context.DisposeAsync();
    }

    protected void MarkTestFailed()
    {
        _testFailed = true;
    }

    internal static async Task WaitForBlazorReadyAsync(IPage page, int timeoutMs = 30_000)
    {
        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-blazor-ready=\"true\"]') !== null",
            null,
            new PageWaitForFunctionOptions { Timeout = timeoutMs, PollingInterval = 250 });
        // Give the circuit a moment to fully initialize after ready signal
        await Task.Delay(500);
    }

    private async Task SaveFailureArtifactsAsync()
    {
        string artifactDir = Path.Combine("test-results", "failures", $"{GetType().Name}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(artifactDir);

        try
        {
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(artifactDir, "screenshot.png"),
                FullPage = true,
            });
        }
        catch
        {
            // Best-effort artifact capture
        }

        try
        {
            string html = await Page.ContentAsync();
            await File.WriteAllTextAsync(Path.Combine(artifactDir, "page.html"), html);
        }
        catch
        {
            // Best-effort artifact capture
        }

        bool tracingEnabled = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("E2E_TRACING"));
        if (tracingEnabled)
        {
            try
            {
                await Context.Tracing.StopAsync(new TracingStopOptions
                {
                    Path = Path.Combine(artifactDir, "trace.zip"),
                });
            }
            catch
            {
                // Best-effort artifact capture
            }
        }

        _testOutputHelper?.WriteLine($"Failure artifacts saved to: {artifactDir}");
    }
}
