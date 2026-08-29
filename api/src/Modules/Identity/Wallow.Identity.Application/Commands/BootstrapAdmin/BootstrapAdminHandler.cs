using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Commands.BootstrapAdmin;

/// <summary>
/// Creates the first administrator AND the organization that makes them one - joining it as
/// owner when the seed already created it. Creating only the
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

        // The seed may already have created the organization named here - production seeds the
        // one the dashboard client is bound to, and no admin - so bootstrap joins it rather than
        // minting a sibling nobody's client points at. Either way the owner membership carrying
        // the admin role is the only grant in this flow, and the only one authorization reads.
        OrganizationDto? existing = await FindOrganizationByNameAsync(command.OrganizationName, ct);

        Guid organizationId;
        if (existing is not null)
        {
            await organizationService.EnrollOwnerAsync(existing.Id, userId, ct);
            organizationId = existing.Id;
            LogAdminEnrolledInExistingOrganization(command.Email, userId, organizationId);
        }
        else
        {
            organizationId = await organizationService.CreateOrganizationAsync(
                command.OrganizationName,
                domain: null,
                creatorEmail: command.Email,
                creatorUserId: userId,
                ct);
        }

        LogAdminBootstrapped(command.Email, userId, organizationId);

        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin user already exists for email {Email}, skipping bootstrap")]
    private partial void LogAdminAlreadyExists(string email);

    /// <summary>
    /// The same match the seeder applies when it binds a client to an organization by name, so a
    /// deployment that seeds "Wallow" and types "wallow" on the setup page lands in one organization.
    /// </summary>
    private async Task<OrganizationDto?> FindOrganizationByNameAsync(string name, CancellationToken ct)
    {
        IReadOnlyList<OrganizationDto> matches = await organizationService.GetOrganizationsAsync(name, ct: ct);
        return matches.FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin user {Email} with ID {UserId} enrolled as owner of existing organization {OrganizationId}")]
    private partial void LogAdminEnrolledInExistingOrganization(string email, Guid userId, Guid organizationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bootstrapped admin user {Email} with ID {UserId} as owner of organization {OrganizationId}")]
    private partial void LogAdminBootstrapped(string email, Guid userId, Guid organizationId);
}
