namespace Wallow.Notifications.Domain.Channels.Push;

public sealed record WebPushSubscription(string Endpoint, WebPushSubscriptionKeys Keys, long? ExpirationTime = null);

public sealed record WebPushSubscriptionKeys(string P256dh, string Auth);
