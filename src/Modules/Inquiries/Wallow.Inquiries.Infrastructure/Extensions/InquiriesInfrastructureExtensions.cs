using Wallow.Inquiries.Application.Interfaces;
using Wallow.Inquiries.Infrastructure.Persistence;
using Wallow.Inquiries.Infrastructure.Persistence.Repositories;
using Wallow.Inquiries.Infrastructure.Services;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Kernel.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Wallow.Inquiries.Infrastructure.Extensions;

public static class InquiriesInfrastructureExtensions
{
    public static IServiceCollection AddInquiriesInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        int maxPoolSize = configuration.GetValue("Database:MaxPoolSize", 200);
        int minPoolSize = configuration.GetValue("Database:MinPoolSize", 10);

        string defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddPooledDbContextFactory<InquiriesDbContext>((sp, options) =>
        {
            NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
            {
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize
            };
            options.UseNpgsql(builder.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "inquiries");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddScoped<InquiriesDbContext>(sp =>
        {
            IDbContextFactory<InquiriesDbContext> factory = sp.GetRequiredService<IDbContextFactory<InquiriesDbContext>>();
            InquiriesDbContext ctx = factory.CreateDbContext();
            ITenantContext tenant = sp.GetRequiredService<ITenantContext>();
            ctx.SetTenant(tenant.TenantId);
            return ctx;
        });

        services.AddReadDbContext<InquiriesDbContext>(configuration);

        services.AddScoped<IInquiryRepository, InquiryRepository>();
        services.AddScoped<IInquiryCommentRepository, InquiryCommentRepository>();
        services.AddSingleton<IRateLimitService, ValkeyRateLimitService>();

        return services;
    }
}
