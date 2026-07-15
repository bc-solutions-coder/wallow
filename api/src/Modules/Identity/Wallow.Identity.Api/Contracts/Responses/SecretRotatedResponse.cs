namespace Wallow.Identity.Api.Contracts.Responses;

public record SecretRotatedResponse
{
    public required string NewClientSecret { get; init; }
    public required DateTime RotatedAt { get; init; }
    public string Warning => "Save this secret now. It will not be shown again.";
}
