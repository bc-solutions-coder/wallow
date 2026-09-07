using System.Collections.ObjectModel;
using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Infrastructure.Options;

/// <summary>
/// Organization enrollment settings applied before pre-registered clients are synced.
/// </summary>
public sealed record SeedOrganizationDefinition
{
    /// <summary>
    /// Name used to find or create the organization.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Enrollment policy to apply. Null preserves the current enrollment settings.
    /// </summary>
    public EnrollmentPolicy? EnrollmentPolicy { get; init; }

    /// <summary>
    /// Access-request address applied with EnrollmentPolicy. Null preserves the current address.
    /// </summary>
    public string? AccessRequestEmail { get; init; }
}

public sealed class SeedOrganizationOptions
{
    public const string SectionName = "Organizations";

    public Collection<SeedOrganizationDefinition> Organizations { get; set; } = [];

    /// <summary>
    /// Rejects entries without an organization name.
    /// </summary>
    public void Validate()
    {
        int offenders = Organizations.Count(o => string.IsNullOrWhiteSpace(o.Name));

        if (offenders > 0)
        {
            throw new InvalidOperationException(
                $"{offenders} seeded organization(s) declare no \"name\". An organization is found or "
                + "created by name, so a nameless entry can only be a typo.");
        }
    }
}
