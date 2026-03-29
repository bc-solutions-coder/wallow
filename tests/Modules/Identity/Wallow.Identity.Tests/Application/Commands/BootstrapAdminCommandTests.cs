using Microsoft.Extensions.Logging;
#pragma warning disable IDE0005
using NSubstitute.ExceptionExtensions;
#pragma warning restore IDE0005
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

    [Fact]
    public async Task Handle_WhenUserAlreadyExists_DoesNotAssignRole()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "Admin", "User");

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        BootstrapAdminHandler handler = new(_bootstrapAdminService, _logger);

        await handler.Handle(command, CancellationToken.None);

        await _bootstrapAdminService.DidNotReceive()
            .AssignRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_EnsuresRoleBeforeCreatingUser()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "Admin", "User");
        Guid createdUserId = Guid.NewGuid();
        List<string> callOrder = [];

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _bootstrapAdminService
            .EnsureRoleExistsAsync("admin", Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callOrder.Add("EnsureRole");
                return Task.CompletedTask;
            });

        _bootstrapAdminService
            .CreateUserAsync(command.Email, command.Password, command.FirstName, command.LastName, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callOrder.Add("CreateUser");
                return createdUserId;
            });

        _bootstrapAdminService
            .AssignRoleAsync(createdUserId, "admin", Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callOrder.Add("AssignRole");
                return Task.CompletedTask;
            });

        BootstrapAdminHandler handler = new(_bootstrapAdminService, _logger);

        await handler.Handle(command, CancellationToken.None);

        callOrder.Should().ContainInOrder("EnsureRole", "CreateUser", "AssignRole");
    }

    [Fact]
    public async Task Handle_WhenCreateUserThrows_PropagatesException()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "Admin", "User");

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _bootstrapAdminService
            .CreateUserAsync(command.Email, command.Password, command.FirstName, command.LastName, Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new InvalidOperationException("User creation failed"));

        BootstrapAdminHandler handler = new(_bootstrapAdminService, _logger);

        Func<Task> act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User creation failed");
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_PassesExactCommandValuesToService()
    {
        BootstrapAdminCommand command = new("specific@test.org", "MyP@ss123!", "Jane", "Doe");
        Guid createdUserId = Guid.NewGuid();

        _bootstrapAdminService
            .UserExistsAsync("specific@test.org", Arg.Any<CancellationToken>())
            .Returns(false);

        _bootstrapAdminService
            .CreateUserAsync("specific@test.org", "MyP@ss123!", "Jane", "Doe", Arg.Any<CancellationToken>())
            .Returns(createdUserId);

        BootstrapAdminHandler handler = new(_bootstrapAdminService, _logger);

        await handler.Handle(command, CancellationToken.None);

        await _bootstrapAdminService.Received(1)
            .CreateUserAsync("specific@test.org", "MyP@ss123!", "Jane", "Doe", Arg.Any<CancellationToken>());
        await _bootstrapAdminService.Received(1)
            .AssignRoleAsync(createdUserId, "admin", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEnsureRoleThrows_PropagatesException()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "Admin", "User");

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _bootstrapAdminService
            .EnsureRoleExistsAsync("admin", Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Role creation failed"));

        BootstrapAdminHandler handler = new(_bootstrapAdminService, _logger);

        Func<Task> act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Role creation failed");
    }

    [Fact]
    public async Task Handle_WhenAssignRoleThrows_PropagatesException()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "Admin", "User");
        Guid createdUserId = Guid.NewGuid();

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _bootstrapAdminService
            .CreateUserAsync(command.Email, command.Password, command.FirstName, command.LastName, Arg.Any<CancellationToken>())
            .Returns(createdUserId);

        _bootstrapAdminService
            .AssignRoleAsync(createdUserId, "admin", Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Role assignment failed"));

        BootstrapAdminHandler handler = new(_bootstrapAdminService, _logger);

        Func<Task> act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Role assignment failed");
    }

    [Fact]
    public async Task Handle_WhenUserExistsCheckThrows_PropagatesException()
    {
        BootstrapAdminCommand command = new("admin@example.com", "P@ssw0rd!", "Admin", "User");

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new InvalidOperationException("Database unavailable"));

        BootstrapAdminHandler handler = new(_bootstrapAdminService, _logger);

        Func<Task> act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database unavailable");
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsSuccess()
    {
        BootstrapAdminCommand command = new("new@example.com", "P@ssw0rd!", "New", "Admin");
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
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_UsesAdminRoleName()
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

        await handler.Handle(command, CancellationToken.None);

        await _bootstrapAdminService.Received(1)
            .EnsureRoleExistsAsync("admin", Arg.Any<CancellationToken>());
        await _bootstrapAdminService.Received(1)
            .AssignRoleAsync(createdUserId, "admin", Arg.Any<CancellationToken>());
    }
}
