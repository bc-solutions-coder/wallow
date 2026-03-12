using Foundry.Messaging.Application.Conversations.Interfaces;
using Foundry.Messaging.Domain.Conversations.Entities;
using Foundry.Shared.Kernel.MultiTenancy;
using Foundry.Shared.Kernel.Results;

namespace Foundry.Messaging.Application.Conversations.Commands.CreateConversation;

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
