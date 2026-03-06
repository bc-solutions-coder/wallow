using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Showcases.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddShowcasesApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
