using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wallow.Identity.Infrastructure.Extensions;
using Wallow.Identity.Infrastructure.Options;

namespace Wallow.Identity.Tests.Infrastructure;

public class EmailChangeOptionsTests
{
    [Fact]
    public void Defaults_AllowThreeRequestsPerHour()
    {
        using ServiceProvider provider = CreateProvider(null, null);

        EmailChangeOptions options = provider.GetRequiredService<IOptions<EmailChangeOptions>>().Value;

        options.RateLimitMaxRequests.Should().Be(3);
        options.RateLimitWindow.Should().Be(TimeSpan.FromHours(1));
    }

    [Theory]
    [InlineData("2", "02:00:00", 2, 120)]
    [InlineData("0", "00:05:00", 0, 5)]
    public void Configuration_BindsCapAndWindow(string cap, string window, int expectedCap, int expectedMinutes)
    {
        using ServiceProvider provider = CreateProvider(cap, window);

        EmailChangeOptions options = provider.GetRequiredService<IOptions<EmailChangeOptions>>().Value;

        options.RateLimitMaxRequests.Should().Be(expectedCap);
        options.RateLimitWindow.Should().Be(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Theory]
    [InlineData("-1", "01:00:00")]
    [InlineData("3", "00:00:00")]
    [InlineData("3", "-01:00:00")]
    public void InvalidConfiguration_IsRejected(string cap, string window)
    {
        using ServiceProvider provider = CreateProvider(cap, window);

        Action read = () => provider.GetRequiredService<IStartupValidator>().Validate();

        read.Should().Throw<OptionsValidationException>();
    }

    private static ServiceProvider CreateProvider(string? cap, string? window)
    {
        Dictionary<string, string?> settings = new()
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=options",
        };
        if (cap is not null)
        {
            settings["Identity:EmailChange:RateLimitMaxRequests"] = cap;
            settings["Identity:EmailChange:RateLimitWindow"] = window;
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Testing");
        ServiceCollection services = new();
        services.AddSingleton(Substitute.For<IConnectionMultiplexer>());
        services.AddIdentityInfrastructure(configuration, environment);
        return services.BuildServiceProvider();
    }
}
