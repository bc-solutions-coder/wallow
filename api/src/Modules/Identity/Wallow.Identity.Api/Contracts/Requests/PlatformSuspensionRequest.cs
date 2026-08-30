using System.ComponentModel.DataAnnotations;

namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Places a platform suspension. The reason is the operator's and travels with the suspension:
/// the affected organization's admins read it, only a global admin removes it. The length cap
/// matches the column both suspension marks persist the reason into.
/// </summary>
public record PlatformSuspensionRequest
{
    [MaxLength(1000)]
    public required string Reason { get; init; }
}
