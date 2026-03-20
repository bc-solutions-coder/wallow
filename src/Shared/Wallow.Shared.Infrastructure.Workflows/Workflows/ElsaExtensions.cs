using Elsa.EntityFrameworkCore.Extensions;
using Elsa.EntityFrameworkCore.Modules.Management;
using Elsa.EntityFrameworkCore.Modules.Runtime;
using Elsa.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Wallow.Shared.Infrastructure.Workflows.Workflows;

public static class ElsaExtensions
{
    public static IServiceCollection AddWallowWorkflows(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        string signingKey = ResolveSigningKey(configuration, environment);

        services.AddElsa(elsa =>
        {
            elsa.UseWorkflowManagement(management =>
            {
                management.UseEntityFrameworkCore(ef => ef.UsePostgreSql(connectionString));

                // Auto-discover module workflow activities from all Wallow assemblies
                IEnumerable<Type> activityTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name?.StartsWith("Wallow.", StringComparison.Ordinal) == true)
                    .SelectMany(a => a.GetExportedTypes())
                    .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && t.IsAssignableTo(typeof(WorkflowActivityBase)));

                management.AddActivities(activityTypes);
            });

            elsa.UseWorkflowRuntime(runtime =>
                runtime.UseEntityFrameworkCore(ef => ef.UsePostgreSql(connectionString)));

            elsa.UseIdentity(identity =>
            {
                identity.TokenOptions = options => options.SigningKey = signingKey;
                identity.UseAdminUserProvider();
            });

            elsa.UseWorkflowsApi();
            elsa.UseScheduling();
            elsa.UseHttp();
            elsa.UseEmail(email =>
                email.ConfigureOptions = options => configuration.GetSection("Elsa:Smtp").Bind(options));
        });

        return services;
    }

    private static string ResolveSigningKey(IConfiguration configuration, IHostEnvironment environment)
    {
        string? configuredKey = configuration["Elsa:Identity:SigningKey"];

        if (!string.IsNullOrEmpty(configuredKey))
        {
            return configuredKey;
        }

        if (environment.IsDevelopment())
        {
            return "wallow-default-elsa-signing-key-replace-in-production";
        }

        throw new InvalidOperationException(
            "Elsa:Identity:SigningKey must be configured in non-Development environments.");
    }
}
