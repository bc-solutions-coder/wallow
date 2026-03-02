using Foundry.Shared.Kernel.Domain;

namespace Foundry.Configuration.Domain.Exceptions;

public class FeatureFlagException : BusinessRuleException
{
    public FeatureFlagException(string message)
        : base("Configuration.FeatureFlag", message)
    {
    }

    public FeatureFlagException()
    {
    }

    public FeatureFlagException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
