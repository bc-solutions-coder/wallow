namespace Wallow.Identity.Api.Contracts.Responses;

/// <summary>
/// Success response for account operations that return no additional data.
/// </summary>
public sealed record AccountOperationResponse(bool Succeeded);
