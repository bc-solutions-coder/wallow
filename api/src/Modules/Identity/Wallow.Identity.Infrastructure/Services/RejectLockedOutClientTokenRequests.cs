using OpenIddict.Server;
using Wallow.Identity.Application.Interfaces;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// A client_id that tripped the invalid_client lockout is refused at the token endpoint for the
/// duration of the lockout, correct secret or not. It runs before OpenIddict validates the
/// client's credentials, so a guesser who finally lands the right secret inside the lockout
/// learns nothing: the answer is the same <c>invalid_client</c> every wrong guess got. The
/// rejection stamps a transaction property so the audit handler can tell the lockout's own
/// refusals apart from genuine authentication failures and not count them.
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
