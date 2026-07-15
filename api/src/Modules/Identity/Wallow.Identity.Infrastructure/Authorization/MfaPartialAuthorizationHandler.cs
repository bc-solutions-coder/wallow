using Microsoft.AspNetCore.Authorization;
using Wallow.Identity.Application.Interfaces;

namespace Wallow.Identity.Infrastructure.Authorization;

public class MfaPartialAuthorizationHandler(IMfaPartialAuthService mfaPartialAuthService)
    : AuthorizationHandler<MfaPartialRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MfaPartialRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            context.Succeed(requirement);
            return;
        }

        MfaPartialAuthPayload? payload = await mfaPartialAuthService.ValidatePartialCookieAsync(CancellationToken.None);
        if (payload is not null)
        {
            context.Succeed(requirement);
        }
    }
}
