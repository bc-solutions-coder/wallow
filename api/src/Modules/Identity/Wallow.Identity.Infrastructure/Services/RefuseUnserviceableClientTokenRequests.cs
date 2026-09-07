using OpenIddict.Server;
using Wallow.Identity.Application.Interfaces;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Applies client-state refusals before OpenIddict authentication, for every token grant.
/// Returns invalid_client with the policy description before validating credentials.
/// </summary>
public sealed class RefuseUnserviceableClientTokenRequests(IClientAccessPolicy clientAccessPolicy)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenRequestContext>()
            .UseScopedHandler<RefuseUnserviceableClientTokenRequests>()
            .SetOrder(OpenIddictServerHandlers.Exchange.ValidateAuthentication.Descriptor.Order - 500)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ClientAccessRefusal? refusal = await clientAccessPolicy.EvaluateAsync(
            context.ClientId, context.CancellationToken);
        if (refusal is null)
        {
            return;
        }

        context.Reject(
            error: Errors.InvalidClient,
            description: refusal.Description);
    }
}
