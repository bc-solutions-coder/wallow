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
    /// <summary>
    /// Schema shared by the model and migration history table.
    /// Internal to keep schema ownership within this module.
    /// </summary>
    internal const string Schema = "storage";

    public string Name => "Storage";

    public bool IsCore => false;

    public IReadOnlyList<Assembly> HandlerAssemblies =>
    [
        typeof(CreateBucketHandler).Assembly,
        typeof(StorageModule).Assembly,
    ];

    public IReadOnlyList<Type> DbContextTypes => [typeof(StorageDbContext)];

    public string SchemaName => Schema;

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services.AddStorageModule(configuration);
    }
}
