using Microsoft.Extensions.Logging;
#pragma warning disable IDE0005
using NSubstitute.ExceptionExtensions;
#pragma warning restore IDE0005
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Tests.Application.Commands;

/// <summary>
/// The first administrator is only an administrator because of a membership: roles resolve per
/// organization, so bootstrap has to create the organization the wizard names and enroll the new
/// user into it as owner. Creating the user and stopping there produced an account holding no
/// permission anywhere, and left the setup gate open forever (Wallow-cr20).
/// </summary>
public class BootstrapAdminCommandTests
{
    private const string OrganizationName = "Acme Inc";

    private readonly IBootstrapAdminService _bootstrapAdminService = Substitute.For<IBootstrapAdminService>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly ILogger<BootstrapAdminHandler> _logger = Substitute.For<ILogger<BootstrapAdminHandler>>();

    private BootstrapAdminHandler CreateHandler() =>
        new(_bootstrapAdminService, _organizationService, _logger);

    private static BootstrapAdminCommand Command(
        string email = "admin@example.com",
        string password = "P@ssw0rd!",
        string firstName = "Admin",
        string lastName = "User") =>
        new(email, password, firstName, lastName, OrganizationName);

    [Fact]
    public async Task Handle_WhenUserAlreadyExists_ReturnsSuccessWithoutCreating()
    {
        BootstrapAdminCommand command = Command();

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        Result result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _bootstrapAdminService.DidNotReceive()
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _bootstrapAdminService.DidNotReceive()
            .EnsureRoleExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyExists_DoesNotCreateAnOrganization()
    {
        BootstrapAdminCommand command = Command();

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        await CreateHandler().Handle(command, CancellationToken.None);

        await _organizationService.DidNotReceive().CreateOrganizationAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_EnrollsTheNewAdminAsTheOrganizationCreator()
    {
        BootstrapAdminCommand command = Command();
        Guid createdUserId = Guid.NewGuid();

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _bootstrapAdminService
            .CreateUserAsync(command.Email, command.Password, command.FirstName, command.LastName, Arg.Any<CancellationToken>())
            .Returns(createdUserId);

        Result result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // creatorUserId is what mints the owner membership carrying the admin role — the only
        // thing that makes this user an administrator, and the only thing the setup gate reads.
        await _organizationService.Received(1).CreateOrganizationAsync(
            OrganizationName,
            Arg.Any<string?>(),
            command.Email,
            createdUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_EnsuresTheAdminRoleExistsBeforeTheOrganization()
    {
        BootstrapAdminCommand command = Command();
        Guid createdUserId = Guid.NewGuid();
        List<string> callOrder = [];

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _bootstrapAdminService
            .EnsureRoleExistsAsync("admin", Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callOrder.Add("EnsureRole");
                return Task.CompletedTask;
            });

        _bootstrapAdminService
            .CreateUserAsync(command.Email, command.Password, command.FirstName, command.LastName, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callOrder.Add("CreateUser");
                return createdUserId;
            });

        _organizationService
            .CreateOrganizationAsync(OrganizationName, Arg.Any<string?>(), command.Email, createdUserId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callOrder.Add("CreateOrganization");
                return Guid.NewGuid();
            });

        await CreateHandler().Handle(command, CancellationToken.None);

        // The organization resolves the admin role by name to enroll its creator, so the catalog
        // entry has to exist before it runs.
        callOrder.Should().ContainInOrder("EnsureRole", "CreateUser", "CreateOrganization");
    }

    [Fact]
    public async Task Handle_WhenCreateUserThrows_PropagatesException()
    {
        BootstrapAdminCommand command = Command();

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _bootstrapAdminService
            .CreateUserAsync(command.Email, command.Password, command.FirstName, command.LastName, Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new InvalidOperationException("User creation failed"));

        Func<Task> act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User creation failed");
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_PassesExactCommandValuesToService()
    {
        BootstrapAdminCommand command = new("specific@test.org", "MyP@ss123!", "Jane", "Doe", "Contoso");
        Guid createdUserId = Guid.NewGuid();

        _bootstrapAdminService
            .UserExistsAsync("specific@test.org", Arg.Any<CancellationToken>())
            .Returns(false);

        _bootstrapAdminService
            .CreateUserAsync("specific@test.org", "MyP@ss123!", "Jane", "Doe", Arg.Any<CancellationToken>())
            .Returns(createdUserId);

        await CreateHandler().Handle(command, CancellationToken.None);

        await _bootstrapAdminService.Received(1)
            .CreateUserAsync("specific@test.org", "MyP@ss123!", "Jane", "Doe", Arg.Any<CancellationToken>());
        await _organizationService.Received(1).CreateOrganizationAsync(
            "Contoso", Arg.Any<string?>(), "specific@test.org", createdUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEnsureRoleThrows_PropagatesException()
    {
        BootstrapAdminCommand command = Command();

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _bootstrapAdminService
            .EnsureRoleExistsAsync("admin", Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Role creation failed"));

        Func<Task> act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Role creation failed");
    }

    [Fact]
    public async Task Handle_WhenOrganizationCreationThrows_PropagatesException()
    {
        BootstrapAdminCommand command = Command();
        Guid createdUserId = Guid.NewGuid();

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _bootstrapAdminService
            .CreateUserAsync(command.Email, command.Password, command.FirstName, command.LastName, Arg.Any<CancellationToken>())
            .Returns(createdUserId);

        _organizationService
            .CreateOrganizationAsync(OrganizationName, Arg.Any<string?>(), command.Email, createdUserId, Arg.Any<CancellationToken>())
            .Returns<Guid>(_ => throw new InvalidOperationException("Organization creation failed"));

        Func<Task> act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Organization creation failed");
    }

    [Fact]
    public async Task Handle_WhenUserExistsCheckThrows_PropagatesException()
    {
        BootstrapAdminCommand command = Command();

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new InvalidOperationException("Database unavailable"));

        Func<Task> act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database unavailable");
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_UsesAdminRoleName()
    {
        BootstrapAdminCommand command = Command();
        Guid createdUserId = Guid.NewGuid();

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _bootstrapAdminService
            .CreateUserAsync(command.Email, command.Password, command.FirstName, command.LastName, Arg.Any<CancellationToken>())
            .Returns(createdUserId);

        await CreateHandler().Handle(command, CancellationToken.None);

        await _bootstrapAdminService.Received(1)
            .EnsureRoleExistsAsync("admin", Arg.Any<CancellationToken>());
    }
}
