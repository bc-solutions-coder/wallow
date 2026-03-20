#pragma warning disable CA1032 // Intentionally restricting constructors to enforce structured exception creation

using Wallow.Shared.Kernel.Domain;

namespace Wallow.Messaging.Domain.Conversations.Exceptions;

public class ConversationException : BusinessRuleException
{
    public ConversationException(string message)
        : base("Messaging.Conversation", message)
    {
    }
}
