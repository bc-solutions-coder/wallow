using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Shared.Kernel.Results;
using Wallow.Tests.Common.Factories;
using Wolverine;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Invokes bootstrap through Wolverine to exercise generated dependency construction under the host policy.
/// </summary>
[Collection(nameof(ApiIntegrationTestCollection))]
[Trait("Category", "Integration")]
public sealed class BootstrapAdminHandlerCodegenTests(WallowApiFactory factory)
{
    [Fact]
    public async Task BootstrapAdminCommand_CompilesAndRunsThroughTheWolverinePipeline()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        BootstrapAdminCommand command = new(
            Email: $"wizard-{Guid.NewGuid():N}@wallow.dev",
            Password: "Wizard1234!",
            FirstName: "Wizard",
            LastName: "Admin",
            OrganizationName: $"Wizard Org {Guid.NewGuid():N}");

        Result result = await bus.InvokeAsync<Result>(command);

        result.IsSuccess.Should().BeTrue(
            "the setup wizard is the only way into an unseeded installation");
    }
}
