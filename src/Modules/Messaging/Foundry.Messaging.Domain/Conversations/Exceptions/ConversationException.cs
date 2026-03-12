#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation

using Foundry.Shared.Kernel.Domain;

namespace Foundry.Messaging.Domain.Conversations.Exceptions;

public class ConversationException : BusinessRuleException
{
    public ConversationException(string message)
        : base("Messaging.Conversation", message)
    {
    }
}
