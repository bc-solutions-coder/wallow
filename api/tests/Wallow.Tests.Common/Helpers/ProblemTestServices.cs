using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Wallow.Shared.Api.Problems;

namespace Wallow.Tests.Common.Helpers;

/// <summary>
/// Request services carrying the host's problem contract, for unit tests that drive a controller
/// or middleware directly. With these on the <c>HttpContext</c>, <c>ValidationProblem</c>, a
/// <c>ProblemResult</c>, and <c>IProblemDetailsService</c> all produce the same body the API
/// serves, so a test can assert on status, code, and shape rather than on a bare object.
/// </summary>
public static class ProblemTestServices
{
    public static IServiceProvider Build(string environment = "Production") =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHostEnvironment>(new TestHostEnvironment(environment))
            .AddMvcCore()
            .Services
            .AddWallowProblemDetails()
            .BuildServiceProvider();

    private sealed class TestHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Wallow.Tests";

        public string WebRootPath { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
