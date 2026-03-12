using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Notifications.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
