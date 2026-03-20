using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Billing.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddBillingApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
