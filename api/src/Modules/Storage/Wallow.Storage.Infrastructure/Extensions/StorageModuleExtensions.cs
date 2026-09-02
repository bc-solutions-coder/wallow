using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Kernel.Errors;
using Wallow.Storage.Application.Extensions;
using Wallow.Storage.Domain.Errors;

namespace Wallow.Storage.Infrastructure.Extensions;

public static class StorageModuleExtensions
{
    public static IServiceCollection AddStorageModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddErrorCatalog(typeof(StorageErrors));
        services.AddStorageApplication();
        services.AddStorageInfrastructure(configuration);
        return services;
    }
}
