using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.ApiKeys.Domain.Errors;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.ApiKeys.Infrastructure.Extensions;

public static class ApiKeysModuleExtensions
{
    public static IServiceCollection AddApiKeysModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddErrorCatalog(typeof(ApiKeysErrors));
        services.AddApiKeysInfrastructure(configuration);
        return services;
    }
}
