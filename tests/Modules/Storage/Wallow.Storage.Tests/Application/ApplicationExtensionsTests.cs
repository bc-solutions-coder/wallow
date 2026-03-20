using FluentValidation;
using Wallow.Storage.Application.Commands.CreateBucket;
using Wallow.Storage.Application.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Wallow.Storage.Tests.Application;

public class ApplicationExtensionsTests
{
    [Fact]
    public void AddStorageApplication_RegistersValidators()
    {
        ServiceCollection services = new();

        services.AddStorageApplication();

        ServiceProvider provider = services.BuildServiceProvider();
        IValidator<CreateBucketCommand> validator = provider.GetRequiredService<IValidator<CreateBucketCommand>>();
        validator.Should().NotBeNull();
        validator.Should().BeOfType<CreateBucketValidator>();
    }
}
