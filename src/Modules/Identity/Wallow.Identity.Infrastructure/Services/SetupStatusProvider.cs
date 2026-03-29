using Wallow.Identity.Application.Queries.IsSetupRequired;
using Wallow.Shared.Contracts.Setup;
using Wolverine;

namespace Wallow.Identity.Infrastructure.Services;

public sealed class SetupStatusProvider(IMessageBus messageBus) : ISetupStatusProvider
{
    public async Task<bool> IsSetupRequiredAsync(CancellationToken cancellationToken = default)
    {
        bool result = await messageBus.InvokeAsync<bool>(new IsSetupRequiredQuery(), cancellationToken);
        return result;
    }
}
