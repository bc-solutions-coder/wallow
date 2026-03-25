using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wallow.Billing.Application.Interfaces;
using Wallow.Billing.Application.Metering.Interfaces;
using Wallow.Billing.Application.Metering.Services;
using Wallow.Billing.Application.Settings;
using Wallow.Billing.Infrastructure.Jobs;
using Wallow.Billing.Infrastructure.Persistence;
using Wallow.Billing.Infrastructure.Persistence.Repositories;
using Wallow.Billing.Infrastructure.Services;
using Wallow.Shared.Contracts.Billing;
using Wallow.Shared.Contracts.Metering;
using Wallow.Shared.Infrastructure.Core.Extensions;
using Wallow.Shared.Infrastructure.Settings;
using Wallow.Shared.Kernel.MultiTenancy;

namespace Wallow.Billing.Infrastructure.Extensions;

public static class BillingInfrastructureExtensions
{
    public static IServiceCollection AddBillingInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBillingPersistence(configuration);
        services.AddReadDbContext<BillingDbContext>(configuration);
        services.AddSettings<BillingDbContext, BillingSettingKeys>("billing");
        return services;
    }

    private static void AddBillingPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        int maxPoolSize = configuration.GetValue("Database:MaxPoolSize", 200);
        int minPoolSize = configuration.GetValue("Database:MinPoolSize", 10);

        string defaultConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddPooledDbContextFactory<BillingDbContext>((sp, options) =>
        {
            NpgsqlConnectionStringBuilder builder = new(defaultConnectionString)
            {
                MaxPoolSize = maxPoolSize,
                MinPoolSize = minPoolSize
            };
            options.UseNpgsql(builder.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "billing");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
            options.AddInterceptors(sp.GetRequiredService<TenantSaveChangesInterceptor>());
        });

        services.AddScoped<BillingDbContext>(sp =>
        {
            IDbContextFactory<BillingDbContext> factory = sp.GetRequiredService<IDbContextFactory<BillingDbContext>>();
            BillingDbContext ctx = factory.CreateDbContext();
            ITenantContext tenant = sp.GetRequiredService<ITenantContext>();
            ctx.SetTenant(tenant.TenantId);
            return ctx;
        });

        // Billing repositories
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();

        // Billing services
        services.AddScoped<IInvoiceQueryService, InvoiceQueryService>();
        services.AddScoped<IInvoiceReportService, InvoiceReportService>();
        services.AddScoped<IPaymentReportService, PaymentReportService>();
        services.AddScoped<IRevenueReportService, RevenueReportService>();
        services.AddScoped<ISubscriptionQueryService, SubscriptionQueryService>();

        // Metering repositories
        services.AddScoped<IMeterDefinitionRepository, MeterDefinitionRepository>();
        services.AddScoped<IQuotaDefinitionRepository, QuotaDefinitionRepository>();
        services.AddScoped<IUsageRecordRepository, UsageRecordRepository>();

        // Metering services
        services.AddScoped<IMeteringService, ValkeyMeteringService>();
        services.AddScoped<IMeteringQueryService, MeteringQueryService>();
        services.AddScoped<IUsageReportService, UsageReportService>();

        // Metering jobs
        services.AddScoped<FlushUsageJob>();

    }
}
