using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Inquiries.Application.Extensions;

namespace Wallow.Inquiries.Tests.Application.Extensions;

public class ApplicationExtensionsTests
{
    [Fact]
    public void AddInquiriesApplication_RegistersValidators()
    {
        ServiceCollection services = new();

        services.AddInquiriesApplication();

        ServiceProvider provider = services.BuildServiceProvider();
        IEnumerable<IValidator> validators = provider.GetServices<IValidator>();

        validators.Should().NotBeNull();
    }

    [Fact]
    public void AddInquiriesApplication_ReturnsServices()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddInquiriesApplication();

        result.Should().BeSameAs(services);
    }
}
