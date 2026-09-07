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
/// Syncs named organizations and their configured enrollment policies before client sync.
/// </summary>
public sealed partial class OrganizationSeedSyncService(
    IOrganizationService organizationService,
    IdentityDbContext dbContext,
    IOptions<SeedOrganizationOptions> options,
    ILogger<OrganizationSeedSyncService> logger)
{
    /// <summary>
    /// Audit actor for seed operations without a user.
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

        // Scope settings reads to this organization, then restore the previous tenant.
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
        // Preserve omitted settings because UpdateEnrollmentAsync writes all three fields.
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
