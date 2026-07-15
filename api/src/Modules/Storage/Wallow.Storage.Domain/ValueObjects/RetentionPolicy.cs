using Wallow.Storage.Domain.Enums;

namespace Wallow.Storage.Domain.ValueObjects;

public sealed record RetentionPolicy(int Days, RetentionAction Action);
