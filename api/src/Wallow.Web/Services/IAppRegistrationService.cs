using Wallow.Web.Models;

namespace Wallow.Web.Services;

public interface IAppRegistrationService
{
    Task<List<AppModel>> GetAppsAsync(CancellationToken ct = default);
    Task<AppModel?> GetAppAsync(string clientId, CancellationToken ct = default);
    Task<RegisterAppResult> RegisterAppAsync(RegisterAppModel model, CancellationToken ct = default);
    Task<bool> UpsertBrandingAsync(string clientId, string displayName, string? tagline, string? themeJson, Stream? logoStream, string? logoFileName, string? logoContentType, CancellationToken ct = default);
}
