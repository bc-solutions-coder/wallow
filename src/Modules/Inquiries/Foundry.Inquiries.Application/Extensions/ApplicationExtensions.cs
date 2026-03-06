using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Inquiries.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddInquiriesApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
