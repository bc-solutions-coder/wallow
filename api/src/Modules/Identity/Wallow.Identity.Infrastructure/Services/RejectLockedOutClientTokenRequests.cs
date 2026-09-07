using OpenIddict.Server;
using Wallow.Identity.Application.Interfaces;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Rejects locked-out client IDs before credential validation, even with the correct secret.
/// Marks the rejection so the audit handler does not count it as another failed guess.
/// </summary>
public sealed class RejectLockedOutClientTokenRequests(IInvalidClientLockout invalidClientLockout)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>
{
    internal const string LockoutRejectionProperty = "wallow:invalid-client-lockout-rejection";

    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenRequestContext>()
            .UseScopedHandler<RejectLockedOutClientTokenRequests>()
            .SetOrder(OpenIddictServerHandlers.Exchange.ValidateAuthentication.Descriptor.Order - 600)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrEmpty(context.ClientId)
            || !await invalidClientLockout.IsLockedOutAsync(context.ClientId, context.CancellationToken))
        {
            return;
        }

        context.Transaction.Properties[LockoutRejectionProperty] = true;
        context.Reject(
            error: Errors.InvalidClient,
            description: "The client is temporarily rejected.");
    }
}
