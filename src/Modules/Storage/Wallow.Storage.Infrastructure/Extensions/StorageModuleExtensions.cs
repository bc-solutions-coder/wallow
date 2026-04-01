using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Storage.Application.Extensions;

namespace Wallow.Storage.Infrastructure.Extensions;

public static class StorageModuleExtensions
{
    public static IServiceCollection AddStorageModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddStorageApplication();
        services.AddStorageInfrastructure(configuration);
        return services;
    }

    public static Task<WebApplication> InitializeStorageModuleAsync(
        this WebApplication app)
    {
        return Task.FromResult(app);
    }
}
