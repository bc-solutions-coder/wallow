using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Inquiries.Application.Extensions;

namespace Wallow.Inquiries.Infrastructure.Extensions;

public static class InquiriesModuleExtensions
{
    public static IServiceCollection AddInquiriesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddInquiriesApplication();
        services.AddInquiriesInfrastructure(configuration);
        return services;
    }

    public static Task<WebApplication> InitializeInquiriesModuleAsync(
        this WebApplication app)
    {
        return Task.FromResult(app);
    }
}
