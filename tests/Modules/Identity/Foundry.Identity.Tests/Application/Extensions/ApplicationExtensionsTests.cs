using FluentValidation;
using Foundry.Identity.Application.Commands.CreateServiceAccount;
using Foundry.Identity.Application.Commands.UpdateServiceAccountScopes;
using Foundry.Identity.Application.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Foundry.Identity.Tests.Application.Extensions;

public class ApplicationExtensionsTests
{
    [Fact]
    public void AddIdentityApplication_RegistersValidatorsFromAssembly()
    {
        ServiceCollection services = new();

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
        ServiceCollection services = new();

        IServiceCollection result = services.AddIdentityApplication();

        result.Should().BeSameAs(services);
    }
}
