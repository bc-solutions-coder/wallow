using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Shared.Infrastructure.Core.Persistence;
using Wallow.Shared.Kernel.Persistence;

namespace Wallow.Shared.Infrastructure.Core.Extensions;

public static class ReadDbContextExtensions
{
    public static IServiceCollection AddReadDbContext<TContext>(
        this IServiceCollection services, IConfiguration configuration)
        where TContext : DbContext
    {
        string? replicaConnection = configuration.GetConnectionString("ReadReplicaConnection");

        string connectionString = string.IsNullOrEmpty(replicaConnection)
            ? configuration.GetConnectionString("DefaultConnection")
              ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.")
            : replicaConnection;

        ReadDbContextFactory<TContext> factory = new(connectionString);
        services.AddSingleton(factory);

        services.AddScoped<IReadDbContext<TContext>>(sp =>
        {
            ReadDbContextFactory<TContext> f = sp.GetRequiredService<ReadDbContextFactory<TContext>>();
            return f.CreateReadDbContext();
        });

        return services;
    }
}
