using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Commands.BootstrapAdmin;

/// <summary>
/// Creates the first administrator AND the organization that makes them one. Creating only the
/// user left an account that resolved no roles anywhere and a setup gate that never closed: the
/// gate asks whether an Active membership holds an admin-granting role, and a bare user has no
/// membership to hold one.
/// </summary>
public sealed partial class BootstrapAdminHandler(
    IBootstrapAdminService bootstrapAdminService,
    IOrganizationService organizationService,
    ILogger<BootstrapAdminHandler> logger)
{
    private const string AdminRoleName = "admin";

    public async Task<Result> Handle(BootstrapAdminCommand command, CancellationToken ct)
    {
        bool userExists = await bootstrapAdminService.UserExistsAsync(command.Email, ct);
        if (userExists)
        {
            LogAdminAlreadyExists(command.Email);
            return Result.Success();
        }

        // Before the organization, which resolves this role by name to enroll its creator.
        await bootstrapAdminService.EnsureRoleExistsAsync(AdminRoleName, ct);

        Guid userId = await bootstrapAdminService.CreateUserAsync(
            command.Email,
            command.Password,
            command.FirstName,
            command.LastName,
            ct);

        // Passing the creator is what mints the owner membership carrying the admin role. It is
        // the only grant in this flow, and the only one authorization reads.
        Guid organizationId = await organizationService.CreateOrganizationAsync(
            command.OrganizationName,
            domain: null,
            creatorEmail: command.Email,
            creatorUserId: userId,
            ct);

        LogAdminBootstrapped(command.Email, userId, organizationId);

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin user already exists for email {Email}, skipping bootstrap")]
    private partial void LogAdminAlreadyExists(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bootstrapped admin user {Email} with ID {UserId} as owner of organization {OrganizationId}")]
    private partial void LogAdminBootstrapped(string email, Guid userId, Guid organizationId);
}
