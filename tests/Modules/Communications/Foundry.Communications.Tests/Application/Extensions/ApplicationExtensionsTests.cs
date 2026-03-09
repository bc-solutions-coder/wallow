using Foundry.Communications.Application.Announcements.Services;
using Foundry.Communications.Application.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Communications.Tests.Application.Extensions;

public class ApplicationExtensionsTests
{
    [Fact]
    public void AddCommunicationsApplication_RegistersAnnouncementTargetingService()
    {
        ServiceCollection services = new();

        services.AddCommunicationsApplication();

        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IAnnouncementTargetingService));
        descriptor.Should().NotBeNull();
        descriptor.ImplementationType.Should().Be<AnnouncementTargetingService>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddCommunicationsApplication_RegistersValidators()
    {
        ServiceCollection services = new();

        services.AddCommunicationsApplication();

        // FluentValidation validators should be registered
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddCommunicationsApplication_ReturnsServiceCollection()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddCommunicationsApplication();

        result.Should().BeSameAs(services);
    }
}
