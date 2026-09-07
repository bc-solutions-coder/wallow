using Microsoft.Extensions.Logging;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Identity.Application.Commands.BootstrapAdmin;

/// <summary>
/// Creates an administrator and enrolls them as owner of a matching organization,
/// or creates that organization. An existing user causes bootstrap to return without changes.
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

        // Reuse a seeded organization so the administrator joins the organization clients already use.
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
    /// Finds a case-insensitive exact name match among organization search results.
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
