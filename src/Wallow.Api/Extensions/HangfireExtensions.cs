using Hangfire;
using Hangfire.PostgreSql;
using Wallow.Api.Middleware;

namespace Wallow.Api.Extensions;

internal static class HangfireExtensions
{
    public static IServiceCollection AddHangfireServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")!;

        services.AddHangfire(config =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(opts =>
                    opts.UseNpgsqlConnection(connectionString),
                    new PostgreSqlStorageOptions
                    {
                        SchemaName = "hangfire"
                    });
        });

        services.AddHangfireServer();

        return services;
    }

    public static WebApplication UseHangfireDashboard(
        this WebApplication app)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new HangfireDashboardAuthFilter(app.Environment)],
            DashboardTitle = "Wallow Jobs"
        });

        return app;
    }

}
