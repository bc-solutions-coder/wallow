using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Storage.Application.Extensions;

public static class ApplicationExtensions
{
    public static IServiceCollection AddStorageApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);
        return services;
    }
}
