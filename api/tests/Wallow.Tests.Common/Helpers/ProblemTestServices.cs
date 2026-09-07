using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Wallow.Shared.Api.Problems;

namespace Wallow.Tests.Common.Helpers;

/// <summary>
/// Builds MVC and problem-details services for direct controller and middleware tests.
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
