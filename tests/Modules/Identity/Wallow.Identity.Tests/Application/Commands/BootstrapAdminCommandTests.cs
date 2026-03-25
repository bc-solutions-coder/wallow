using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Tests.Application.Commands;

public class BootstrapAdminCommandTests
{
    private readonly IBootstrapAdminService _bootstrapAdminService = Substitute.For<IBootstrapAdminService>();
    private readonly ILogger<BootstrapAdminHandler> _logger = Substitute.For<ILogger<BootstrapAdminHandler>>();

    [Fact]
    public async Task Handle_WhenUserAlreadyExists_ReturnsSuccessWithoutCreating()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "Admin", "User");

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        BootstrapAdminHandler handler = new(_bootstrapAdminService, _logger);

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _bootstrapAdminService.DidNotReceive()
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _bootstrapAdminService.DidNotReceive()
            .EnsureRoleExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_CreatesAdminWithRole()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "Admin", "User");
        Guid createdUserId = Guid.NewGuid();

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _bootstrapAdminService
            .CreateUserAsync(command.Email, command.Password, command.FirstName, command.LastName, Arg.Any<CancellationToken>())
            .Returns(createdUserId);

        BootstrapAdminHandler handler = new(_bootstrapAdminService, _logger);

        Result result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await _bootstrapAdminService.Received(1)
            .EnsureRoleExistsAsync("admin", Arg.Any<CancellationToken>());

        await _bootstrapAdminService.Received(1)
            .CreateUserAsync(command.Email, command.Password, command.FirstName, command.LastName, Arg.Any<CancellationToken>());

        await _bootstrapAdminService.Received(1)
            .AssignRoleAsync(createdUserId, "admin", Arg.Any<CancellationToken>());
    }
}
