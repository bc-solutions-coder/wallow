using System.Diagnostics;
using System.Net;

namespace Wallow.E2E.Tests.Fixtures;

public sealed class DockerComposeFixture : IAsyncLifetime
{
    private const string ComposeFilePath = "docker/docker-compose.test.yml";
    private static readonly TimeSpan _healthCheckTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _healthCheckInterval = TimeSpan.FromSeconds(3);

    public string ApiBaseUrl { get; } = Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5050";
    public string AuthBaseUrl { get; } = Environment.GetEnvironmentVariable("E2E_AUTH_URL") ?? "http://localhost:5051";
    public string WebBaseUrl { get; } = Environment.GetEnvironmentVariable("E2E_WEB_URL") ?? "http://localhost:5053";

    /// <summary>
    /// When true, containers are managed externally (CI) — skip docker compose up/down.
    /// Set by the E2E_EXTERNAL_SERVICES environment variable or auto-detected when services are already healthy.
    /// </summary>
    private bool _externalServices;

    public async Task InitializeAsync()
    {
        _externalServices = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("E2E_EXTERNAL_SERVICES"))
            || await AreServicesAlreadyHealthyAsync();

        if (!_externalServices)
        {
            string composeFile = GetComposeFilePath();
            await RunDockerComposeAsync($"-f \"{composeFile}\" up -d --build");
        }

        using HttpClient httpClient = new();
        httpClient.Timeout = TimeSpan.FromSeconds(5);

        await WaitForHealthAsync(httpClient, $"{ApiBaseUrl}/health/ready", "API");
        await WaitForHealthAsync(httpClient, $"{AuthBaseUrl}/health", "Auth");
        await WaitForHealthAsync(httpClient, $"{WebBaseUrl}/health", "Web");
    }

    public async Task DisposeAsync()
    {
        if (!_externalServices)
        {
            string composeFile = GetComposeFilePath();
            await RunDockerComposeAsync($"-f \"{composeFile}\" down -v");
        }
    }

    private async Task<bool> AreServicesAlreadyHealthyAsync()
    {
        using HttpClient httpClient = new();
        httpClient.Timeout = TimeSpan.FromSeconds(2);

        try
        {
            HttpResponseMessage response = await httpClient.GetAsync($"{ApiBaseUrl}/health/ready");
            return response.StatusCode == HttpStatusCode.OK;
        }
        catch
        {
            return false;
        }
    }

    private static string GetComposeFilePath()
    {
        // Walk up from the test output directory to find the repo root
        string? directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            string candidate = Path.Combine(directory, ComposeFilePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new FileNotFoundException(
            $"Could not find {ComposeFilePath} relative to {AppContext.BaseDirectory}");
    }

    private static async Task RunDockerComposeAsync(string arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "docker",
            Arguments = $"compose {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start docker compose process.");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            string stderr = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException(
                $"docker compose {arguments} failed with exit code {process.ExitCode}: {stderr}");
        }
    }

    private static async Task WaitForHealthAsync(HttpClient httpClient, string url, string serviceName)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < _healthCheckTimeout)
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(url);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (Exception) when (stopwatch.Elapsed < _healthCheckTimeout)
            {
                // Service not ready yet, retry
            }

            await Task.Delay(_healthCheckInterval);
        }

        throw new TimeoutException(
            $"{serviceName} at {url} did not become healthy within {_healthCheckTimeout.TotalSeconds} seconds.");
    }
}
