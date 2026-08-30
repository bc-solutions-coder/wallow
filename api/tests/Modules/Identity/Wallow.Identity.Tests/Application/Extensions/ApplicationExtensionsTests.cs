using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Extensions;

namespace Wallow.Identity.Tests.Application.Extensions;

public class ApplicationExtensionsTests
{
    [Fact]
    public void AddIdentityApplication_RegistersValidatorsFromAssembly()
    {
        ServiceCollection services = new ServiceCollection();

        services.AddIdentityApplication();

        ServiceProvider provider = services.BuildServiceProvider();
        IValidator<BootstrapAdminCommand> validator =
            provider.GetRequiredService<IValidator<BootstrapAdminCommand>>();

        validator.Should().BeOfType<BootstrapAdminValidator>();
    }

    [Fact]
    public void AddIdentityApplication_ReturnsServiceCollection()
    {
        ServiceCollection services = new ServiceCollection();

        IServiceCollection result = services.AddIdentityApplication();

        result.Should().BeSameAs(services);
    }
}
