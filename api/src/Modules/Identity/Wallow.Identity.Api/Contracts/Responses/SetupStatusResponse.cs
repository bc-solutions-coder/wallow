namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Whether first-run setup is open. OrganizationName is offered only while setup is open
/// and exactly one organization exists.
/// </summary>
public sealed record SetupStatusResponse(bool SetupRequired, string? OrganizationName = null);
