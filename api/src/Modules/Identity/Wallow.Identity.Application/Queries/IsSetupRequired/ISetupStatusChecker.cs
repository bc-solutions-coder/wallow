namespace Wallow.Identity.Application.Queries.IsSetupRequired;

/// <summary>
/// Checks whether the initial admin setup has been completed.
/// Implemented in Infrastructure using ASP.NET Core Identity.
/// </summary>
public interface ISetupStatusChecker
{
    Task<bool> IsSetupRequiredAsync(CancellationToken ct = default);
}
