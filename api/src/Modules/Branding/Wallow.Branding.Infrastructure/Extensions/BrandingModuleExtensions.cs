using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Branding.Domain.Errors;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Branding.Infrastructure.Extensions;

public static class BrandingModuleExtensions
{
    public static IServiceCollection AddBrandingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddErrorCatalog(typeof(BrandingErrors));
        services.AddBrandingInfrastructure(configuration);
        return services;
    }
}
