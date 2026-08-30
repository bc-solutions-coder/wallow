using OpenIddict.Server;
using Wallow.Identity.Application.Interfaces;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// A client the platform will not serve is refused at the token endpoint whatever it asks for — a
/// code exchange, a refresh, its own credentials — with <c>invalid_client</c> and the refusal's
/// own sentence: suspended by its organization or by the platform, or bound to an organization
/// that is archived or platform-suspended. It runs before OpenIddict authenticates the request,
/// which is where the client secret and the presented grant are checked together, so a refresh
/// token a revocation ended is answered as "this client is out of service" rather than "this
/// token is dead". Nothing about the client is disclosed that its id alone does not already name.
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
