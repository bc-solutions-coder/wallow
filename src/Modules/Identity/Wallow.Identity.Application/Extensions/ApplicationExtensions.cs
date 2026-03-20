using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Identity.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
