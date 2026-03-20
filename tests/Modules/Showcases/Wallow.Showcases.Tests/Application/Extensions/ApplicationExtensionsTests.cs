using FluentValidation;
using Wallow.Showcases.Application.Commands.CreateShowcase;
using Wallow.Showcases.Application.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Showcases.Tests.Application.Extensions;

public class ApplicationExtensionsTests
{
    [Fact]
    public void AddShowcasesApplication_RegistersCreateShowcaseValidator()
    {
        ServiceCollection services = new();

        services.AddShowcasesApplication();

        ServiceProvider provider = services.BuildServiceProvider();
        IValidator<CreateShowcaseCommand>? validator = provider.GetService<IValidator<CreateShowcaseCommand>>();

        validator.Should().NotBeNull();
        validator.Should().BeOfType<CreateShowcaseValidator>();
    }

    [Fact]
    public void AddShowcasesApplication_RegistersUpdateShowcaseValidator()
    {
        ServiceCollection services = new();

        services.AddShowcasesApplication();

        ServiceProvider provider = services.BuildServiceProvider();
        IValidator<Wallow.Showcases.Application.Commands.UpdateShowcase.UpdateShowcaseCommand>? validator =
            provider.GetService<IValidator<Wallow.Showcases.Application.Commands.UpdateShowcase.UpdateShowcaseCommand>>();

        validator.Should().NotBeNull();
    }

    [Fact]
    public void AddShowcasesApplication_ReturnsServiceCollection()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddShowcasesApplication();

        result.Should().BeSameAs(services);
    }
}
