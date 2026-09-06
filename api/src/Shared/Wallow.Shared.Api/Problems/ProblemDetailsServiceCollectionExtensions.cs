using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wallow.Shared.Api.Problems;

/// <summary>
/// Registers the problem contract: the customizer, the single writer, camelCase validation keys for
/// automatic 400s, and the problem-response OpenAPI convention.
/// </summary>
public static class ProblemDetailsServiceCollectionExtensions
{
    /// <summary>
    /// Installs the problem contract. Call after the MVC registration so the
    /// <see cref="MvcOptions"/> configuration applies to it.
    /// </summary>
    public static IServiceCollection AddWallowProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemContract.Customize);
        services.RemoveAll<IProblemDetailsWriter>();
        services.AddSingleton<IProblemDetailsWriter, WallowProblemDetailsWriter>();

        services.Configure<MvcOptions>(options =>
        {
            options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider());
            options.Conventions.Add(new ProblemResponsesConvention());
        });

        return services;
    }
}
