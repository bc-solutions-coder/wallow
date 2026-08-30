namespace Wallow.Shared.Kernel.Identity.Authorization;

/// <summary>
/// Marks an endpoint an organization-less token may reach. A first-party token issued without
/// an organization hint to a user who belongs to several organizations (or to none) carries no
/// <c>org_id</c>; every tenant-scoped endpoint refuses it with 403, and only endpoints carrying
/// this marker (profile, my organizations, create organization, accept invitation) answer it.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class AllowWithoutOrganizationAttribute : Attribute;
