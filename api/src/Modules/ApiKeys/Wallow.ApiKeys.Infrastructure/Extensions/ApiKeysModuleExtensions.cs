using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.ApiKeys.Infrastructure.Extensions;

public static class ApiKeysModuleExtensions
{
    public static IServiceCollection AddApiKeysModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApiKeysInfrastructure(configuration);
        return services;
    }

    public static Task<WebApplication> InitializeApiKeysModuleAsync(
        this WebApplication app)
    {
        return Task.FromResult(app);
    }
}
