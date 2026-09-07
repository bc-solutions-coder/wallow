using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using OpenIddict.Server;
using Wallow.Identity.Application.Interfaces;
using Wallow.Shared.Kernel.Auditing;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Wallow.Identity.Infrastructure.Services;

/// <summary>
/// Audits invalid_client token responses with a client ID, then increments its failure
/// counter. Skips marked lockout rejections so they do not extend their own lockout.
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
