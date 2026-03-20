using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Inquiries.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddInquiriesApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
