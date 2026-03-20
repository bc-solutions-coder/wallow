using JetBrains.Annotations;

namespace Wallow.Messaging.Api.Contracts.Messaging.Responses;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record MessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string Body,
    string Status,
    DateTimeOffset SentAt);
