namespace Wallow.Identity.Application.Queries.IsSetupRequired;

public sealed class IsSetupRequiredHandler(ISetupStatusChecker setupStatusChecker)
{
    public Task<bool> Handle(IsSetupRequiredQuery _)
    {
        return setupStatusChecker.IsSetupRequiredAsync();
    }
}
