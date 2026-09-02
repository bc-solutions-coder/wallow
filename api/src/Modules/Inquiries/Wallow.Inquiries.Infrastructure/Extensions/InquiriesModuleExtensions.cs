using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Inquiries.Application.Extensions;
using Wallow.Inquiries.Domain.Errors;
using Wallow.Shared.Kernel.Errors;

namespace Wallow.Inquiries.Infrastructure.Extensions;

public static class InquiriesModuleExtensions
{
    public static IServiceCollection AddInquiriesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddErrorCatalog(typeof(InquiriesErrors));
        services.AddInquiriesApplication();
        services.AddInquiriesInfrastructure(configuration);
        return services;
    }
}
