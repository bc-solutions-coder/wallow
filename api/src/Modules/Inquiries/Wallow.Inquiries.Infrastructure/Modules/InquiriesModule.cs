using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wallow.Inquiries.Application.Commands.SubmitInquiry;
using Wallow.Inquiries.Infrastructure.Extensions;
using Wallow.Inquiries.Infrastructure.Persistence;
using Wallow.Shared.Infrastructure.Modules;

namespace Wallow.Inquiries.Infrastructure.Modules;

public sealed class InquiriesModule : IWallowModule
{
    /// <summary>
    /// The one place this module's Postgres schema name is written. <see cref="SchemaName"/>, the
    /// module's <c>HasDefaultSchema</c> and every host's <c>MigrationsHistoryTable</c> all resolve
    /// to this constant, so the compiler rather than a convention is what keeps them equal.
    /// </summary>
    /// <remarks>
    /// Internal on purpose: every consumer — this class, the module's <c>DbContext</c> and its
    /// infrastructure extensions — lives in <c>Wallow.Inquiries.Infrastructure</c>. Keeping it
    /// internal stops any other assembly, another module included, from taking a compile-time
    /// dependency on this module's schema name.
    /// </remarks>
    internal const string Schema = "inquiries";

    public string Name => "Inquiries";

    public bool IsCore => false;

    public IReadOnlyList<Assembly> HandlerAssemblies =>
    [
        typeof(SubmitInquiryHandler).Assembly,
        typeof(InquiriesModule).Assembly,
    ];

    public IReadOnlyList<Type> DbContextTypes => [typeof(InquiriesDbContext)];

    public string SchemaName => Schema;

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services.AddInquiriesModule(configuration);
    }
}
