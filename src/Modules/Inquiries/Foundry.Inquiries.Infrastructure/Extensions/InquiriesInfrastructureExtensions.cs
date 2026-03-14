using Foundry.Inquiries.Application.Interfaces;
using Foundry.Inquiries.Infrastructure.Persistence;
using Foundry.Inquiries.Infrastructure.Persistence.Repositories;
using Foundry.Inquiries.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Inquiries.Infrastructure.Extensions;

public static class InquiriesInfrastructureExtensions
{
    public static IServiceCollection AddInquiriesInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<InquiriesDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "inquiries");
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
            });
        });

        services.AddScoped<IInquiryRepository, InquiryRepository>();
        services.AddScoped<IInquiryCommentRepository, InquiryCommentRepository>();
        services.AddSingleton<IRateLimitService, ValkeyRateLimitService>();

        return services;
    }
}
