using Foundry.Shared.Kernel.Domain;

namespace Foundry.Communications.Domain.Exceptions;

public class ConversationException : BusinessRuleException
{
    public ConversationException(string message)
        : base("Communications.Conversation", message)
    {
    }

    public ConversationException()
    {
    }

    public ConversationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
