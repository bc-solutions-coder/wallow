namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Whether first-run setup is still open and, while it is, the organization the bootstrap
/// administrator will own. <paramref name="OrganizationName"/> is the one organization the seed
/// already created (the one the dashboard client is bound to) so the setup page can offer it
/// rather than let the visitor type a name that creates a sibling; <see langword="null"/> when
/// setup is complete or when there is not exactly one organization to offer.
/// </summary>
public sealed record SetupStatusResponse(bool SetupRequired, string? OrganizationName = null);
