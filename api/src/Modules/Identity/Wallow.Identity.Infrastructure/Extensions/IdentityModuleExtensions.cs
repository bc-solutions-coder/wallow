using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Identity.Application.Extensions;
using Wallow.Identity.Domain.Errors;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Identity.Infrastructure.Extensions;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddErrorCatalog(typeof(IdentityErrors));
        services.AddIdentityApplication();
        services.AddIdentityInfrastructure(configuration, environment);
        return services;
    }
}
