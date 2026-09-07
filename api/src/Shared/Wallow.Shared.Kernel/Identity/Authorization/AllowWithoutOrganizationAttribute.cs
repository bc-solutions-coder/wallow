namespace Wallow.Shared.Kernel.Identity.Authorization;

/// <summary>
/// Allows an authenticated token without <c>org_id</c> to reach an endpoint.
/// Tenant resolution otherwise rejects such tokens on endpoints requiring an organization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class AllowWithoutOrganizationAttribute : Attribute;
