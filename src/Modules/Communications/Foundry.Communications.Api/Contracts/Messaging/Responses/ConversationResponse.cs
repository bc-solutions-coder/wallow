using Foundry.Communications.Application.Messaging.DTOs;
using JetBrains.Annotations;

namespace Foundry.Communications.Api.Contracts.Messaging.Responses;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ConversationResponse(
    Guid Id,
    string Type,
    IReadOnlyList<ParticipantDto> Participants,
    MessageDto? LastMessage,
    int UnreadCount,
    DateTimeOffset LastActivityAt);
