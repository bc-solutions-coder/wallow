using Microsoft.AspNetCore.Authorization;

namespace Wallow.Identity.Api.Authorization;

public sealed class AuthorizeMfaPartialAttribute : AuthorizeAttribute
{
    public AuthorizeMfaPartialAttribute()
    {
        Policy = "MfaPartial";
    }
}
