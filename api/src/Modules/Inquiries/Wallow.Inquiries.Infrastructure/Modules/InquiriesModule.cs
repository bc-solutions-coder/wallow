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
    public string Name => "Inquiries";

    public bool IsCore => false;

    public IReadOnlyList<Assembly> HandlerAssemblies =>
    [
        typeof(SubmitInquiryHandler).Assembly,
        typeof(InquiriesModule).Assembly,
    ];

    public IReadOnlyList<Type> DbContextTypes => [typeof(InquiriesDbContext)];

    public string SchemaName => "inquiries";

    public IServiceCollection AddServices(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        return services.AddInquiriesModule(configuration);
    }
}
