using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using OpenIddict.Server;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Auditing;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Observes every <c>invalid_client</c> token response on its way out — OpenIddict's own
/// bad-secret rejections included, which no custom validator ever sees — and turns each into a
/// userless <c>ClientAuthenticationFailed</c> audit row plus one tick of the per-client failure
/// counter. The lockout's own refusals are skipped: counting them would let five bad guesses
/// re-arm the lockout forever off rejections that prove nothing new about the caller.
/// </summary>
public sealed class AuditInvalidClientTokenResponses(
    IInvalidClientLockout invalidClientLockout,
    IAuthAuditService authAuditService,
    TimeProvider timeProvider)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ApplyTokenResponseContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ApplyTokenResponseContext>()
            .UseScopedHandler<AuditInvalidClientTokenResponses>()
            .SetOrder(int.MinValue + 100_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    public async ValueTask HandleAsync(OpenIddictServerEvents.ApplyTokenResponseContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? clientId = context.Request?.ClientId;
        if (context.Error != Errors.InvalidClient
            || string.IsNullOrEmpty(clientId)
            || context.Transaction.Properties.ContainsKey(
                RejectLockedOutClientTokenRequests.LockoutRejectionProperty))
        {
            return;
        }

        HttpContext? httpContext = context.Transaction.GetHttpRequest()?.HttpContext;
        await authAuditService.RecordAsync(new AuthAuditRecord
        {
            EventType = "ClientAuthenticationFailed",
            UserId = null,
            ClientId = clientId,
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
            OccurredAt = timeProvider.GetUtcNow(),
        }, context.CancellationToken);

        await invalidClientLockout.RecordFailureAsync(clientId, context.CancellationToken);
    }
}
