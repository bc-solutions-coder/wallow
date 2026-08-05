using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Shared.Infrastructure.Modules;
using Wallow.Storage.Application.Commands.CreateBucket;
using Wallow.Storage.Infrastructure.Extensions;
using Wallow.Storage.Infrastructure.Persistence;

namespace Wallow.Storage.Infrastructure.Modules;

public sealed class StorageModule : IWallowModule
{
    public string Name => "Storage";

    public bool IsCore => false;

    public IEnumerable<Assembly> HandlerAssemblies =>
    [
        typeof(CreateBucketHandler).Assembly,
        typeof(StorageModule).Assembly,
    ];

    public IReadOnlyList<Type> DbContextTypes => [typeof(StorageDbContext)];

    public string SchemaName => "storage";

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services.AddStorageModule(configuration);
    }
}
