using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Enums;
using Wallow.Identity.Infrastructure.Options;
using Wallow.Identity.Infrastructure.Persistence;
using Wallow.Shared.Kernel.Identity;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Brings the organizations named in the seed file, and the terms on which they admit people, up
/// to what the file declares.
/// <para>
/// Runs before client sync so that a client binding to an organization by name finds one whose
/// enrollment policy is already the configured one. A new organization defaults to
/// <see cref="EnrollmentPolicy.InviteOnly"/>, so without this step every seeded organization
/// silently admits nobody but invitees however the fork configured it.
/// </para>
/// </summary>
public sealed partial class OrganizationSeedSyncService(
    IOrganizationService organizationService,
    IdentityDbContext dbContext,
    IOptions<SeedOrganizationOptions> options,
    ILogger<OrganizationSeedSyncService> logger)
{
    /// <summary>
    /// The seeder acts as the system, not as a person. Guid.Empty is the same non-user this
    /// module's other system-initiated writes stamp their audit fields with.
    /// </summary>
    private static readonly Guid _systemActorId = Guid.Empty;

    public async Task SyncAsync(CancellationToken ct)
    {
        SeedOrganizationOptions config = options.Value;
        config.Validate();

        foreach (SeedOrganizationDefinition organization in config.Organizations)
        {
            await SyncOrganizationAsync(organization, ct);
        }
    }

    private async Task SyncOrganizationAsync(SeedOrganizationDefinition definition, CancellationToken ct)
    {
        Guid organizationId = await ResolveOrganizationIdAsync(definition.Name, ct);

        if (definition.EnrollmentPolicy is not { } policy)
        {
            return;
        }

        // The DbContext's tenant is what the EF query filters read, and the seeder runs outside any
        // request, so nothing has set it. Scoped to the organization being seeded, the settings read
        // below sees its row and the write stamps the tenant on a row created for the first time.
        // Restored afterwards so the scope does not leak into the next organization's lookup.
        TenantId previousTenant = dbContext.CurrentTenantId;
        dbContext.SetTenant(TenantId.Create(organizationId));

        try
        {
            await ApplyEnrollmentAsync(definition, organizationId, policy, ct);
        }
        finally
        {
            dbContext.SetTenant(previousTenant);
        }
    }

    private async Task ApplyEnrollmentAsync(
        SeedOrganizationDefinition definition,
        Guid organizationId,
        EnrollmentPolicy policy,
        CancellationToken ct)
    {
        // Read before writing: UpdateEnrollmentAsync writes all three enrollment fields at once,
        // so a seed that names only a policy would otherwise clear the default role and the
        // access-request address an administrator set through the API.
        OrganizationSettingsDto? current = await organizationService.GetSettingsAsync(organizationId, ct);
        string? accessRequestEmail = definition.AccessRequestEmail ?? current?.AccessRequestEmail;

        if (current is not null
            && current.EnrollmentPolicy == policy
            && string.Equals(current.AccessRequestEmail, accessRequestEmail, StringComparison.Ordinal))
        {
            return;
        }

        await organizationService.UpdateEnrollmentAsync(
            organizationId,
            policy,
            accessRequestEmail,
            current?.DefaultRoleId,
            _systemActorId,
            ct);

        LogEnrollmentPolicyApplied(definition.Name, policy);
    }

    private async Task<Guid> ResolveOrganizationIdAsync(string name, CancellationToken ct)
    {
        IReadOnlyList<OrganizationDto> matches = await organizationService.GetOrganizationsAsync(name, ct: ct);
        OrganizationDto? match = matches.FirstOrDefault(
            o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            return match.Id;
        }

        Guid organizationId = await organizationService.CreateOrganizationAsync(name, ct: ct);
        LogOrganizationCreated(name);

        return organizationId;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created seeded organization {OrganizationName}")]
    private partial void LogOrganizationCreated(string organizationName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Organization {OrganizationName} now admits people by {EnrollmentPolicy}")]
    private partial void LogEnrollmentPolicyApplied(string organizationName, EnrollmentPolicy enrollmentPolicy);
}
