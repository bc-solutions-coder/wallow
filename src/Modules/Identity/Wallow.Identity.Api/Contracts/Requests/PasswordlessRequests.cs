namespace Wallow.Identity.Api.Contracts.Requests;

public sealed record SendMagicLinkRequest(string Email);

public sealed record VerifyMagicLinkRequest(string Token);

public sealed record SendOtpRequest(string Email);

public sealed record VerifyOtpRequest(string Email, string Code);
