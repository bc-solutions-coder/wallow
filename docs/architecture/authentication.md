# Authentication

Wallow uses a **sign-in ticket** pattern to bridge the Blazor Auth app (a SignalR-based Blazor Server app) with the API's cookie authentication system. Because Blazor Server runs over a WebSocket circuit, the API cannot directly set `HttpOnly` cookies on the browser from a JSON response. Instead, the API issues an encrypted, short-lived ticket that the browser exchanges for a real auth cookie via a server-side redirect.

---

## Why Tickets Exist

Blazor Server communicates with the API over `HttpClient`. Any `Set-Cookie` headers in an API JSON response are invisible to the browser — they apply to the `HttpClient`'s internal cookie jar on the server, not the user's browser.

To set a browser cookie, the browser itself must make the request. The sign-in ticket is the mechanism that lets the Blazor app hand off authentication to a direct browser navigation, which then hits the API's `exchange-ticket` endpoint. That endpoint validates the ticket and issues a proper `HttpOnly` auth cookie to the browser.

---

## Ticket Lifecycle

```
Wallow.Auth (Blazor)              Wallow.Api (AccountController)
─────────────────────────────     ──────────────────────────────────
POST /identity/auth/login    ───► Validate credentials
                             ◄─── Return { signInTicket: "<token>" }

Navigate browser to:
  /identity/auth/exchange-ticket
  ?ticket=<token>
  &returnUrl=<url>           ───► 1. Decrypt + validate ticket
                                  2. SET NX ticket:used:{jti} in Redis (90s TTL)
                                  3. If key already existed → reject (replay)
                                  4. SignInAsync → issue browser auth cookie
                             ◄─── 302 Redirect to returnUrl
```

### Creation

`CreateSignInTicket` runs in `AccountController` after credentials are successfully verified:

```csharp
private const string TicketPurpose = "SignInTicket";
private static readonly TimeSpan _ticketLifetime = TimeSpan.FromSeconds(60);

private string CreateSignInTicket(string email, bool rememberMe)
{
    ITimeLimitedDataProtector protector = dataProtectionProvider
        .CreateProtector(TicketPurpose)
        .ToTimeLimitedDataProtector();

    SignInTicketPayload payload = new(email, rememberMe, Guid.NewGuid());
    string json = JsonSerializer.Serialize(payload);
    return protector.Protect(json, _ticketLifetime);
}

private sealed record SignInTicketPayload(string Email, bool RememberMe, Guid Jti);
```

- **`Jti`** (JWT ID): a `Guid.NewGuid()` unique identifier embedded in every ticket. Used as the Redis key for single-use enforcement.
- **TTL**: 60 seconds. After this window the data protector refuses to decrypt the token.
- **Purpose**: `"SignInTicket"` — ASP.NET Core Data Protection uses the purpose string as part of the encryption key derivation. A ticket encrypted for `"SignInTicket"` cannot be decrypted by any other protector purpose (e.g., `"ExternalLogin"`).

### Data Protection

ASP.NET Core's `ITimeLimitedDataProtector` wraps the standard `IDataProtector` with an expiry timestamp embedded in the ciphertext. The protector:

1. Encrypts the JSON payload using AES-256-CBC with HMACSHA256 authentication.
2. Encodes the expiry as part of the protected payload.
3. Rejects decryption attempts after the expiry has passed (throws `CryptographicException`).

The encryption keys are managed by the Data Protection system (stored in the configured key ring — typically a shared volume or Redis in production). All API instances share the same key ring, so any instance can validate a ticket issued by another.

### Exchange and Single-Use Enforcement

`ExchangeTicket` in `AccountController` handles the browser's direct GET request:

```csharp
[HttpGet("exchange-ticket")]
[AllowAnonymous]
public async Task<IActionResult> ExchangeTicket([FromQuery] string ticket, [FromQuery] string? returnUrl)
{
    SignInTicketPayload? payload = ValidateSignInTicket(ticket);
    if (payload is null)
    {
        return BadRequest(new { succeeded = false, error = "invalid_or_expired_ticket" });
    }

    // Replay prevention: each ticket can only be exchanged once
    IDatabase redisDb = redis.GetDatabase();
    bool wasSet = await redisDb.StringSetAsync(
        $"ticket:used:{payload.Jti}", "1", TimeSpan.FromSeconds(90), false, When.NotExists);
    if (!wasSet)
    {
        return Unauthorized(new { succeeded = false, error = "ticket_already_used" });
    }

    WallowUser? user = await signInManager.UserManager.FindByEmailAsync(payload.Email);
    if (user is null)
    {
        return BadRequest(new { succeeded = false, error = "invalid_or_expired_ticket" });
    }

    await signInManager.SignInAsync(user, isPersistent: payload.RememberMe);
    // ... redirect
}
```

The Redis `SET NX EX` call is atomic:

- `NX` (Not eXists): only sets the key if it does not already exist.
- `EX 90` (expiry): the key auto-expires after 90 seconds, slightly longer than the ticket TTL, to cover clock skew between the issuance time and exchange time.
- If `wasSet` is `false`, the key was already present — the ticket has already been used. The request is rejected as a replay.

This guarantees that even if an attacker intercepts the ticket URL (e.g., from browser history or a proxy log), replaying it returns `401 ticket_already_used`.

---

## When Tickets Are Issued

Tickets are issued in three scenarios within `AccountController`:

| Scenario | Endpoint | Notes |
|----------|----------|-------|
| Password login, no MFA | `POST /login` | Standard flow |
| Password login, org MFA grace period active | `POST /login` | Ticket issued; enrollment banner shown |
| MFA challenge passed | `POST /mfa/verify` | Issued after TOTP or backup code verification |

Tickets are **not** issued for:
- Passwordless flows (magic link / OTP) — these verify identity differently and trigger sign-in directly after the user returns to the auth app.
- External OAuth provider logins — the callback is a direct browser GET, so the API can set cookies immediately.

---

## Client-Side Exchange (Blazor)

After receiving a successful login response containing a ticket, `Login.razor` checks for a `ReturnUrl` and performs a forced browser navigation to the exchange endpoint:

```csharp
if (!string.IsNullOrEmpty(result.SignInTicket))
{
    string exchangeUrl = $"{ApiBaseUrl}/api/v1/identity/auth/exchange-ticket"
        + $"?ticket={Uri.EscapeDataString(result.SignInTicket)}"
        + $"&returnUrl={Uri.EscapeDataString(ReturnUrl)}";
    Navigation.NavigateTo(exchangeUrl, forceLoad: true);
    return;
}
```

`forceLoad: true` causes a full browser navigation (not a Blazor client-side route change), which is required so the browser actually sends the GET and receives the `Set-Cookie` response header.

---

## Security Properties

| Property | Mechanism |
|----------|-----------|
| Confidentiality | AES-256-CBC encryption via Data Protection |
| Integrity | HMACSHA256 authentication tag |
| Expiry | 60-second TTL enforced by `ITimeLimitedDataProtector` |
| Single-use | Redis SET NX — atomic, guaranteed at most one exchange |
| Purpose isolation | `"SignInTicket"` purpose string prevents cross-purpose decryption |
| Replay TTL headroom | Redis key expires at 90s (>60s ticket TTL) to handle clock skew |

---

## Key Files

| File | Role |
|------|------|
| `src/Modules/Identity/Wallow.Identity.Api/Controllers/AccountController.cs` | `CreateSignInTicket`, `ValidateSignInTicket`, `ExchangeTicket` endpoint |
| `src/Wallow.Auth/Components/Pages/Login.razor` | Ticket exchange navigation logic |
| `src/Wallow.Auth/Components/Pages/MfaChallenge.razor` | Ticket exchange after MFA verification |
| `src/Wallow.Auth/Models/AuthResponse.cs` | `SignInTicket` field on the response DTO |
