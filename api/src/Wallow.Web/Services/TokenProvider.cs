using Microsoft.AspNetCore.Authentication;

namespace Wallow.Web.Services;

public sealed class TokenProvider(IHttpContextAccessor httpContextAccessor)
{
    private string? _accessToken;
    private bool _initialized;

    public string? AccessToken
    {
        get
        {
            if (!_initialized && httpContextAccessor.HttpContext is { } context)
            {
                _accessToken = context.GetTokenAsync("access_token").GetAwaiter().GetResult();
                _initialized = true;
            }
            return _accessToken;
        }
        set
        {
            _accessToken = value;
            _initialized = true;
        }
    }
}
