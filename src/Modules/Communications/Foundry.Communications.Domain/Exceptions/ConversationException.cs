#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation

using Foundry.Shared.Kernel.Domain;

namespace Foundry.Communications.Domain.Exceptions;

public class ConversationException : BusinessRuleException
{
    public ConversationException(string message)
        : base("Communications.Conversation", message)
    {
    }
}
