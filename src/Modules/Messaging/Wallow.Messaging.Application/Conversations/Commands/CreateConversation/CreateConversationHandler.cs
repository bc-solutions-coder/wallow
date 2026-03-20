using Wallow.Messaging.Application.Conversations.Interfaces;
using Wallow.Messaging.Domain.Conversations.Entities;
using Wallow.Shared.Kernel.MultiTenancy;
using Wallow.Shared.Kernel.Results;

namespace Wallow.Messaging.Application.Conversations.Commands.CreateConversation;

public sealed class CreateConversationHandler(
    IConversationRepository conversationRepository,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
{
    public async Task<Result<Guid>> Handle(
        CreateConversationCommand command,
        CancellationToken cancellationToken)
    {
        Conversation conversation = command.Type switch
        {
            "Direct" => Conversation.CreateDirect(
                tenantContext.TenantId,
                command.InitiatorId,
                command.RecipientId!.Value,
                timeProvider),
            "Group" => Conversation.CreateGroup(
                tenantContext.TenantId,
                command.InitiatorId,
                command.Name!,
                command.MemberIds!,
                timeProvider),
            _ => throw new ArgumentException($"Unknown conversation type: {command.Type}", nameof(command))
        };

        conversationRepository.Add(conversation);
        await conversationRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(conversation.Id.Value);
    }
}
