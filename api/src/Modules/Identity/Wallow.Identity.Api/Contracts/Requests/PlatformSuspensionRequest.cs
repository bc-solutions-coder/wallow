using System.ComponentModel.DataAnnotations;

namespace Wallow.Identity.Api.Contracts.Requests;

/// <summary>
/// Reason recorded with a platform suspension, limited to the storage column maximum of 1000 characters.
/// </summary>
public record PlatformSuspensionRequest
{
    [MaxLength(1000)]
    public required string Reason { get; init; }
}
