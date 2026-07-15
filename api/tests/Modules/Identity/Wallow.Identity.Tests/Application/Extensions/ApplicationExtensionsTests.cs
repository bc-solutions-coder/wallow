using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Commands.CreateServiceAccount;
using Wallow.Identity.Application.Commands.UpdateServiceAccountScopes;
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
        IValidator<CreateServiceAccountCommand> createValidator =
            provider.GetRequiredService<IValidator<CreateServiceAccountCommand>>();
        IValidator<UpdateServiceAccountScopesCommand> updateValidator =
            provider.GetRequiredService<IValidator<UpdateServiceAccountScopesCommand>>();

        createValidator.Should().BeOfType<CreateServiceAccountValidator>();
        updateValidator.Should().BeOfType<UpdateServiceAccountScopesValidator>();
    }

    [Fact]
    public void AddIdentityApplication_ReturnsServiceCollection()
    {
        ServiceCollection services = new ServiceCollection();

        IServiceCollection result = services.AddIdentityApplication();

        result.Should().BeSameAs(services);
    }
}
