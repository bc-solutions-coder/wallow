using Microsoft.Extensions.DependencyInjection;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Shared.Kernel.Results;
using Wallow.Tests.Common.Factories;
using Wolverine;

namespace Wallow.Api.Tests.Integration;

/// <summary>
/// Guards the first-run wizard against the codegen policy. <c>ServiceLocationPolicy.NotAllowed</c>
/// is evaluated when Wolverine compiles a handler — the first time the message is sent — so a
/// dependency the codegen cannot inline-construct does not surface at startup or in a unit test
/// that news the handler up itself. It surfaces as a 500 on the one request a brand new
/// installation has to make, which is the last place anyone can afford to find it.
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
