using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Branding.Infrastructure.Extensions;

public static class BrandingModuleExtensions
{
    public static IServiceCollection AddBrandingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddBrandingInfrastructure(configuration);
        return services;
    }

    public static Task<WebApplication> InitializeBrandingModuleAsync(
        this WebApplication app)
    {
        return Task.FromResult(app);
    }
}
