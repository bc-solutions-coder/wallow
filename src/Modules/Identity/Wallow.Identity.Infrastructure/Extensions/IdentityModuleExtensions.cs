using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Identity.Application.Extensions;

namespace Wallow.Identity.Infrastructure.Extensions;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddIdentityApplication();
        services.AddIdentityInfrastructure(configuration, environment);
        return services;
    }

    public static Task<WebApplication> InitializeIdentityModuleAsync(
        this WebApplication app)
    {
        return Task.FromResult(app);
    }
}
