namespace Wallow.Identity.Application.Queries.IsSetupRequired;

/// <summary>
/// Reports whether an active membership with an admin-granting role is still needed.
/// </summary>
public interface ISetupStatusChecker
{
    Task<bool> IsSetupRequiredAsync(CancellationToken ct = default);
}
