namespace Wallow.Shared.Contracts.Setup;

public interface ISetupStatusProvider
{
    Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default);
}
