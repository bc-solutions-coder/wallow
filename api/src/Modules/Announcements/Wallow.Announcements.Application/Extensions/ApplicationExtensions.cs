using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Announcements.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddAnnouncementsApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
