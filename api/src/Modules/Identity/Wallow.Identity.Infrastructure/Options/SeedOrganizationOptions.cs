using System.Collections.ObjectModel;
using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Infrastructure.Options;

/// <summary>
/// A seeded organization and the terms on which it admits people.
/// <para>
/// Enrollment is a property of the organization, not of any client that points at it: three
/// pre-registered clients name the same <c>Wallow</c> organization, and letting each declare a
/// policy would let them disagree about who may join it.
/// </para>
/// </summary>
public sealed record SeedOrganizationDefinition
{
    /// <summary>
    /// The organization's name, matched case-insensitively. The organization is created when no
    /// organization of that name exists, exactly as a client's <c>tenantName</c> creates one.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// How this organization admits non-members. Omitting it leaves whatever policy the
    /// organization already carries — a seed run must not quietly reopen an org an
    /// administrator has since locked down.
    /// </summary>
    public EnrollmentPolicy? EnrollmentPolicy { get; init; }

    /// <summary>
    /// Where access requests are sent. Omitting it preserves the organization's current address
    /// rather than clearing it.
    /// </summary>
    public string? AccessRequestEmail { get; init; }
}

public sealed class SeedOrganizationOptions
{
    public const string SectionName = "Organizations";

    public Collection<SeedOrganizationDefinition> Organizations { get; set; } = [];

    /// <summary>
    /// Fails fast on a nameless organization: there is nothing to find or create it by, and the
    /// enrollment policy declared beside it would silently apply to nothing.
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
