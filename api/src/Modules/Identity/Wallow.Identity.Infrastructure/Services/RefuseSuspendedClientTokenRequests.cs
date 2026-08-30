using OpenIddict.Server;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Enums;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// A suspended client is refused at the token endpoint whatever it asks for — a code exchange, a
/// refresh, its own credentials — with <c>invalid_client</c>. It runs before OpenIddict
/// authenticates the request, which is where the client secret and the presented grant are
/// checked together, so a refresh token the suspension revoked is answered as "this client is out
/// of service" rather than "this token is dead". Nothing about the client is disclosed that its
/// id alone does not already name.
/// </summary>
public sealed class RefuseSuspendedClientTokenRequests(IRegisteredClientRepository registeredClients)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenRequestContext>()
            .UseScopedHandler<RefuseSuspendedClientTokenRequests>()
            .SetOrder(OpenIddictServerHandlers.Exchange.ValidateAuthentication.Descriptor.Order - 500)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrEmpty(context.ClientId))
        {
            return;
        }

        RegisteredClient? registered = await registeredClients.GetByClientIdAsync(
            context.ClientId, context.CancellationToken);
        if (registered?.Status != RegisteredClientStatus.Suspended)
        {
            return;
        }

        context.Reject(
            error: Errors.InvalidClient,
            description: "The client is suspended.");
    }
}
