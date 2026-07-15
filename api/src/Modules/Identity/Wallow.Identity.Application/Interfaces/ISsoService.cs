using Wallow.Identity.Application.DTOs;

namespace Wallow.Identity.Application.Interfaces;

public interface ISsoService
{
    Task<SsoConfigurationDto?> GetConfigurationAsync(CancellationToken ct = default);

    Task<SsoConfigurationDto> SaveOidcConfigurationAsync(SaveOidcConfigRequest request, CancellationToken ct = default);

    Task<SsoTestResult> TestConnectionAsync(CancellationToken ct = default);

    Task ActivateAsync(CancellationToken ct = default);

    Task DisableAsync(CancellationToken ct = default);

    Task<OidcCallbackInfo> GetOidcCallbackInfoAsync(CancellationToken ct = default);

    Task<SsoValidationResult> ValidateIdpConfigurationAsync(CancellationToken ct = default);
}
