using Microsoft.Extensions.Logging;
#pragma warning disable IDE0005
using NSubstitute.ExceptionExtensions;
#pragma warning restore IDE0005
using Wallow.Identity.Application.Commands.BootstrapAdmin;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Tests.Application.Commands;

/// <summary>
/// Checks user bootstrap with organization ownership and reuse of a matching organization.
/// </summary>
public class BootstrapAdminCommandTests
{
    private const string OrganizationName = "Acme Inc";

    private readonly IBootstrapAdminService _bootstrapAdminService = Substitute.For<IBootstrapAdminService>();
    private readonly IOrganizationService _organizationService = Substitute.For<IOrganizationService>();
    private readonly ILogger<BootstrapAdminHandler> _logger = Substitute.For<ILogger<BootstrapAdminHandler>>();

    public BootstrapAdminCommandTests()
    {
        // Default to the first-run case with no matching organization.
        _organizationService
            .GetOrganizationsAsync(Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
    }

    private BootstrapAdminHandler CreateHandler() =>
        new(_bootstrapAdminService, _organizationService, _logger);

    private static OrganizationDto Organization(string name) =>
        new(Guid.NewGuid(), name, null, 0);

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

        // Organization creation needs the new user ID to enroll its owner.
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

        // Owner enrollment resolves the admin role by name.
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

    /// <summary>
    /// Reuses a case-insensitive name match so bootstrap can enroll into a seeded organization.
    /// </summary>
    [Fact]
    public async Task Handle_WhenAnOrganizationWithThatNameExists_EnrollsTheAdminAsItsOwnerInsteadOfCreatingAnother()
    {
        BootstrapAdminCommand command = Command();
        Guid createdUserId = Guid.NewGuid();
        OrganizationDto seeded = Organization("acme inc");

        _bootstrapAdminService
            .UserExistsAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);
        _bootstrapAdminService
            .CreateUserAsync(command.Email, command.Password, command.FirstName, command.LastName, Arg.Any<CancellationToken>())
            .Returns(createdUserId);
        _organizationService
            .GetOrganizationsAsync(OrganizationName, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Organization("Acme Inc Holdings"), seeded]);

        Result result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await _organizationService.Received(1).EnrollOwnerAsync(seeded.Id, createdUserId, Arg.Any<CancellationToken>());
        await _organizationService.DidNotReceive().CreateOrganizationAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenOnlyDifferentlyNamedOrganizationsExist_CreatesTheNamedOne()
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
            .GetOrganizationsAsync(OrganizationName, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Organization("Acme Inc Holdings")]);

        await CreateHandler().Handle(command, CancellationToken.None);

        await _organizationService.DidNotReceive().EnrollOwnerAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _organizationService.Received(1).CreateOrganizationAsync(
            OrganizationName, Arg.Any<string?>(), command.Email, createdUserId, Arg.Any<CancellationToken>());
    }
}
