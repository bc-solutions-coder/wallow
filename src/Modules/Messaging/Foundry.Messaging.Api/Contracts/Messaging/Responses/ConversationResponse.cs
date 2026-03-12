using Foundry.Messaging.Application.Conversations.DTOs;
using JetBrains.Annotations;

namespace Foundry.Messaging.Api.Contracts.Messaging.Responses;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ConversationResponse(
    Guid Id,
    string Type,
    IReadOnlyList<ParticipantDto> Participants,
    MessageDto? LastMessage,
    int UnreadCount,
    DateTimeOffset LastActivityAt);
