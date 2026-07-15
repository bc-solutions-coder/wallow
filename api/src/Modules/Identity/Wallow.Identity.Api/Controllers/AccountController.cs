using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Wallow.Identity.Api.Contracts.Requests;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Extensions;
using Wolverine;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// Cookie-based authentication endpoints for Wallow.Auth (browser-based auth flows).
/// Handles cookie-based browser authentication flows.
/// </summary>
[ApiController]
[ApiVersion(1)]
[Route("v{version:apiVersion}/identity/auth")]
[EnableRateLimiting("auth")]
public sealed partial class AccountController(
    SignInManager<WallowUser> signInManager,
    IConfiguration configuration,
    IRedirectUriValidator redirectUriValidator,
    IDataProtectionProvider dataProtectionProvider,
    IAuthenticationSchemeProvider authSchemeProvider,
    IMessageBus messageBus,
    IClientTenantResolver clientTenantResolver,
    IOrganizationService organizationService,
    IPasswordlessService passwordlessService,
    IMfaExemptionChecker mfaExemptionChecker,
    IMfaService mfaService,
    IMfaPartialAuthService mfaPartialAuthService,
    IOrganizationMfaPolicyService orgMfaPolicyService,
    IMfaLockoutService mfaLockoutService,
    IConnectionMultiplexer redis,
    ILogger<AccountController> logger,
    TimeProvider timeProvider) : ControllerBase
{
    private const string TicketPurpose = "SignInTicket";
    private static readonly TimeSpan _ticketLifetime = TimeSpan.FromSeconds(60);

    [HttpGet("external-providers")]
    [AllowAnonymous]
    public async Task<IActionResult> GetExternalProviders()
    {
        IEnumerable<AuthenticationScheme> schemes = await signInManager.GetExternalAuthenticationSchemesAsync();
        List<string> providers = schemes
            .Select(s => s.DisplayName ?? s.Name)
            .ToList();
        return Ok(providers);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] AccountLoginRequest request, CancellationToken ct)
    {
        LogLoginAttempt(request.Email);
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        WallowUser? user = await signInManager.UserManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            LogLoginUserNotFound(request.Email);
            await messageBus.PublishAsync(new UserLoginFailedEvent
            {
                UserId = Guid.Empty,
                TenantId = Guid.Empty,
                IpAddress = ipAddress,
                Reason = "account_not_found"
            });
            return Unauthorized(new { succeeded = false, error = "invalid_credentials" });
        }

        Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.CheckPasswordSignInAsync(
            user, request.Password, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            LogLoginPasswordValid(user.Email!);

            // User has MFA enabled and is not exempt — issue partial cookie, require MFA verification
            if (user.MfaEnabled && !await mfaExemptionChecker.IsExemptAsync(user, ct))
            {
                LogLoginMfaRequired(user.Email!);
                await mfaPartialAuthService.IssuePartialCookieAsync(
                    new MfaPartialAuthPayload(user.Id.ToString(), user.Email!, "password", request.RememberMe, timeProvider.GetUtcNow()),
                    ct);
                return Ok(new { succeeded = false, mfaRequired = true });
            }

            // Check org-level MFA policy for users who haven't enrolled yet
            OrgMfaPolicyResult? orgPolicy = await orgMfaPolicyService.CheckAsync(user.Id, ct);
            if (orgPolicy is { RequiresMfa: true })
            {
                if (orgPolicy.IsInGracePeriod)
                {
                    LogLoginMfaGracePeriod(user.Email!);
                    // Grace period active — issue ticket so browser can exchange it for a cookie, but flag enrollment needed
                    await messageBus.PublishAsync(new UserLoginSucceededEvent
                    {
                        UserId = user.Id,
                        TenantId = user.TenantId,
                        IpAddress = ipAddress
                    });
                    string graceTicket = CreateSignInTicket(user.Email!, request.RememberMe);
                    return Ok(new { succeeded = true, mfaEnrollmentRequired = true, mfaGraceDeadline = user.MfaGraceDeadline, signInTicket = graceTicket });
                }

                LogLoginMfaEnrollmentRequired(user.Email!);
                // Grace expired — block full sign-in, issue partial cookie
                await mfaPartialAuthService.IssuePartialCookieAsync(
                    new MfaPartialAuthPayload(user.Id.ToString(), user.Email!, "password", request.RememberMe, timeProvider.GetUtcNow()),
                    ct);
                return Ok(new { succeeded = false, mfaEnrollmentRequired = true });
            }

            // No MFA needed — sign in normally
            await messageBus.PublishAsync(new UserLoginSucceededEvent
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                IpAddress = ipAddress
            });
            string ticket = CreateSignInTicket(user.Email!, request.RememberMe);
            LogLoginSucceededWithTicket(user.Email!);
            return Ok(new { succeeded = true, signInTicket = ticket });
        }

        if (result.IsLockedOut)
        {
            await messageBus.PublishAsync(new UserAccountLockedOutEvent
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                IpAddress = ipAddress
            });
            return StatusCode(423, new { succeeded = false, error = "locked_out" });
        }

        if (result.IsNotAllowed)
        {
            return StatusCode(403, new { succeeded = false, error = "email_not_confirmed" });
        }

        await messageBus.PublishAsync(new UserLoginFailedEvent
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            IpAddress = ipAddress,
            Reason = "invalid_credentials"
        });
        return Unauthorized(new { succeeded = false, error = "invalid_credentials" });
    }

    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyMfaChallenge([FromBody] Contracts.Requests.MfaVerifyRequest request, CancellationToken ct)
    {
        string? mfaIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        MfaPartialAuthPayload? payload = await mfaPartialAuthService.ValidatePartialCookieAsync(ct);
        if (payload is null)
        {
            return Unauthorized(new { succeeded = false, error = "no_mfa_session" });
        }

        WallowUser? user = await signInManager.UserManager.FindByIdAsync(payload.UserId);
        if (user is null || string.IsNullOrEmpty(user.TotpSecretEncrypted))
        {
            return Unauthorized(new { succeeded = false, error = "invalid_code" });
        }

        // Check if user is already locked out from MFA attempts
        if (user.IsMfaLockedOut(timeProvider))
        {
            await messageBus.PublishAsync(new UserMfaLockedOutEvent
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                LockoutCount = user.MfaLockoutCount
            });
            return StatusCode(423, new { succeeded = false, error = "mfa_locked_out" });
        }

        // Try TOTP first, then fall back to backup code
        bool isValid = await mfaService.ValidateTotpAsync(user.TotpSecretEncrypted, request.Code, ct)
                       || await mfaService.ValidateBackupCodeAsync(payload.UserId, request.Code, ct);

        if (!isValid)
        {
            MfaLockoutResult lockoutResult = await mfaLockoutService.RecordFailureAsync(user.Id, 5, ct);
            if (lockoutResult.IsLockedOut)
            {
                await messageBus.PublishAsync(new UserMfaLockedOutEvent
                {
                    UserId = user.Id,
                    TenantId = user.TenantId,
                    LockoutCount = lockoutResult.LockoutCount
                });
                return StatusCode(423, new { succeeded = false, error = "mfa_locked_out" });
            }

            await messageBus.PublishAsync(new UserLoginFailedEvent
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                IpAddress = mfaIpAddress,
                Reason = "invalid_mfa_code"
            });
            return Unauthorized(new { succeeded = false, error = "invalid_code" });
        }

        await mfaLockoutService.ResetAsync(user.Id, ct);
        await mfaPartialAuthService.UpgradeToFullAuthAsync(payload.UserId, payload.RememberMe, ct);

        await messageBus.PublishAsync(new UserLoginSucceededEvent
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            IpAddress = mfaIpAddress
        });

        string? email = user.Email;
        string? signInTicket = email is not null ? CreateSignInTicket(email, payload.RememberMe) : null;
        return Ok(new { succeeded = true, signInTicket });
    }

    [HttpGet("external-login")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLogin([FromQuery] string provider, [FromQuery] string returnUrl)
    {
        if (string.IsNullOrEmpty(provider))
        {
            return BadRequest(new { error = "provider_required" });
        }

        AuthenticationScheme? scheme = await authSchemeProvider.GetSchemeAsync(provider);
        if (scheme is null)
        {
            return BadRequest(new { error = "unsupported_provider" });
        }

        string authUrl = GetRequiredAuthUrl();

        if (string.IsNullOrEmpty(returnUrl) || !await redirectUriValidator.IsAllowedAsync(returnUrl))
        {
            return Redirect($"{authUrl}/error?reason=invalid_redirect_uri");
        }

        string callbackUrl = Url.Action(nameof(ExternalLoginCallback), new { returnUrl })!;
        AuthenticationProperties properties = signInManager.ConfigureExternalAuthenticationProperties(provider, callbackUrl);
        return Challenge(properties, provider);
    }

    [HttpGet("external-login-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback([FromQuery] string returnUrl)
    {
        string authUrl = GetRequiredAuthUrl();

        // Validate returnUrl to prevent open redirect attacks
        if (string.IsNullOrEmpty(returnUrl) || !await redirectUriValidator.IsAllowedAsync(returnUrl))
        {
            returnUrl = authUrl;
        }

        ExternalLoginInfo? info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            LogExternalLoginFailed("unknown", "No external login info available");
            return Redirect($"{authUrl}/login?error=external_login_failed");
        }

        // Path A: Existing linked account — sign in directly
        Microsoft.AspNetCore.Identity.SignInResult signInResult = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        if (signInResult.Succeeded)
        {
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            string? signedInEmail = ExternalLoginClaimsHelper.ExtractEmail(info.Principal.Claims);
            WallowUser? signedInUser = signedInEmail is not null
                ? await signInManager.UserManager.FindByEmailAsync(signedInEmail)
                : null;

            if (signedInUser is not null && signedInUser.MfaEnabled
                && !await mfaExemptionChecker.IsExemptAsync(signedInUser, HttpContext.RequestAborted))
            {
                await HttpContext.SignOutAsync();
                await mfaPartialAuthService.IssuePartialCookieAsync(
                    new MfaPartialAuthPayload(
                        signedInUser.Id.ToString(),
                        signedInEmail!,
                        $"external:{info.LoginProvider}",
                        false,
                        timeProvider.GetUtcNow()),
                    HttpContext.RequestAborted);

                string encodedReturn = Uri.EscapeDataString(returnUrl);
                return Redirect($"{authUrl}/mfa/challenge?returnUrl={encodedReturn}");
            }

            // Check org-level MFA policy for users who haven't enrolled yet
            if (signedInUser is not null && !signedInUser.MfaEnabled)
            {
                OrgMfaPolicyResult? orgPolicy = await orgMfaPolicyService.CheckAsync(signedInUser.Id, HttpContext.RequestAborted);
                if (orgPolicy is { RequiresMfa: true })
                {
                    if (!orgPolicy.IsInGracePeriod)
                    {
                        await HttpContext.SignOutAsync();
                        await mfaPartialAuthService.IssuePartialCookieAsync(
                            new MfaPartialAuthPayload(
                                signedInUser.Id.ToString(),
                                signedInEmail!,
                                $"external:{info.LoginProvider}",
                                false,
                                timeProvider.GetUtcNow()),
                            HttpContext.RequestAborted);

                        string encodedReturn = Uri.EscapeDataString(returnUrl);
                        return Redirect($"{authUrl}/mfa/challenge?returnUrl={encodedReturn}");
                    }
                }
            }

            return Redirect(returnUrl);
        }

        string? email = ExternalLoginClaimsHelper.ExtractEmail(info.Principal.Claims);
        if (string.IsNullOrEmpty(email))
        {
            LogExternalLoginFailed(info.LoginProvider, "No email claim from provider");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return Redirect($"{authUrl}/login?error=external_login_failed");
        }

        bool emailVerified = ExternalLoginClaimsHelper.IsEmailVerified(info.Principal.Claims);

        // Path B: Existing account with matching verified email — auto-link
        WallowUser? existingUser = await signInManager.UserManager.FindByEmailAsync(email);
        if (existingUser is not null && emailVerified)
        {
            IdentityResult addLoginResult = await signInManager.UserManager.AddLoginAsync(existingUser, info);
            if (addLoginResult.Succeeded)
            {
                await signInManager.SignInAsync(existingUser, isPersistent: false);
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                return Redirect(returnUrl);
            }

            LogExternalLoginFailed(info.LoginProvider, "Failed to link external login to existing account");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            return Redirect($"{authUrl}/login?error=external_login_failed");
        }

        // Path C: New user — store info in temp cookie, redirect to accept-terms
        (string firstName, string lastName) = ExternalLoginClaimsHelper.ExtractName(info.Principal.Claims, email);

        IDataProtector protector = dataProtectionProvider.CreateProtector("ExternalLogin");
        byte[] plainBytes = System.Text.Encoding.UTF8.GetBytes(
            $"{info.LoginProvider}|{info.ProviderKey}|{email}|{firstName}|{lastName}|{emailVerified}");
        string cookieValue = System.Text.Encoding.UTF8.GetString(protector.Protect(plainBytes));

        Response.Cookies.Append("ExternalLoginState", cookieValue, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10),
            IsEssential = true
        });

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        string encodedReturnUrl = Uri.EscapeDataString(returnUrl);
        string encodedEmail = Uri.EscapeDataString(email);
        string encodedName = Uri.EscapeDataString($"{firstName} {lastName}");
        return Redirect($"{authUrl}/accept-terms?returnUrl={encodedReturnUrl}&email={encodedEmail}&name={encodedName}");
    }

    [HttpGet("complete-external-registration")]
    [AllowAnonymous]
    public async Task<IActionResult> CompleteExternalRegistration(
        [FromQuery] bool acceptedTerms,
        [FromQuery] string returnUrl)
    {
        string authUrl = GetRequiredAuthUrl();

        // Validate returnUrl early, before any user creation
        string validatedReturnUrl = authUrl;
        if (!string.IsNullOrEmpty(returnUrl) && await redirectUriValidator.IsAllowedAsync(returnUrl))
        {
            validatedReturnUrl = returnUrl;
        }

        if (!acceptedTerms)
        {
            return Redirect($"{authUrl}/accept-terms?error=terms_required&returnUrl={Uri.EscapeDataString(validatedReturnUrl)}");
        }

        string? cookieValue = Request.Cookies["ExternalLoginState"];
        if (string.IsNullOrEmpty(cookieValue))
        {
            return Redirect($"{authUrl}/login?error=session_expired");
        }

        string decrypted;
        try
        {
            IDataProtector protector = dataProtectionProvider.CreateProtector("ExternalLogin");
            // Try raw byte decryption first, then fall back to base64url string decryption
            try
            {
                byte[] decryptedBytes = protector.Unprotect(
                    System.Text.Encoding.UTF8.GetBytes(cookieValue));
                decrypted = System.Text.Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                decrypted = protector.Unprotect(cookieValue);
            }
        }
        catch (Exception)
        {
            Response.Cookies.Delete("ExternalLoginState");
            return Redirect($"{authUrl}/login?error=session_expired");
        }

        string[] parts = decrypted.Split('|');
        if (parts.Length < 6)
        {
            Response.Cookies.Delete("ExternalLoginState");
            return Redirect($"{authUrl}/login?error=session_expired");
        }

        string loginProvider = parts[0];
        string providerKey = parts[1];
        string email = parts[2];
        string firstName = parts[3];
        string lastName = parts[4];
        bool emailVerified = bool.TryParse(parts[5], out bool ev) && ev;

        // Check if account was created between callback and ToS acceptance
        WallowUser? existingUser = await signInManager.UserManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            await signInManager.UserManager.AddLoginAsync(
                existingUser, new UserLoginInfo(loginProvider, providerKey, loginProvider));
            await signInManager.SignInAsync(existingUser, isPersistent: false);
            Response.Cookies.Delete("ExternalLoginState");
            return Redirect(validatedReturnUrl);
        }

        WallowUser user = WallowUser.Create(
            tenantId: Guid.Empty,
            firstName: firstName,
            lastName: lastName,
            email: email,
            timeProvider: timeProvider);

        IdentityResult createResult = await signInManager.UserManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            LogExternalLoginFailed(loginProvider, $"Failed to create user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            Response.Cookies.Delete("ExternalLoginState");
            return Redirect($"{authUrl}/login?error=external_login_failed");
        }

        if (emailVerified)
        {
            string token = await signInManager.UserManager.GenerateEmailConfirmationTokenAsync(user);
            await signInManager.UserManager.ConfirmEmailAsync(user, token);
        }

        IdentityResult addLoginResult = await signInManager.UserManager.AddLoginAsync(
            user, new UserLoginInfo(loginProvider, providerKey, loginProvider));

        if (!addLoginResult.Succeeded)
        {
            LogExternalLoginFailed(loginProvider, "Failed to add external login to new user");
            Response.Cookies.Delete("ExternalLoginState");
            return Redirect($"{authUrl}/login?error=external_login_failed");
        }

        await messageBus.PublishAsync(new UserRegisteredEvent
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = null
        });

        if (emailVerified)
        {
            await messageBus.PublishAsync(new EmailVerifiedEvent
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName
            });
        }
        else
        {
            string token = await signInManager.UserManager.GenerateEmailConfirmationTokenAsync(user);
            string verifyUrl = $"{authUrl}/verify-email/confirm?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}";

            if (!string.IsNullOrEmpty(validatedReturnUrl) && validatedReturnUrl != authUrl)
            {
                verifyUrl += $"&returnUrl={Uri.EscapeDataString(validatedReturnUrl)}";
            }

            LogEmailVerificationRequested(user.Email!);

            await messageBus.PublishAsync(new EmailVerificationRequestedEvent
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                Email = user.Email!,
                FirstName = user.FirstName,
                VerifyUrl = verifyUrl
            });
        }

        // Check org-level MFA policy for new user — set grace deadline if MFA required
        OrgMfaPolicyResult? newUserOrgPolicy = await orgMfaPolicyService.CheckAsync(user.Id, HttpContext.RequestAborted);
        if (newUserOrgPolicy is { RequiresMfa: true })
        {
            user.SetMfaGraceDeadline(timeProvider.GetUtcNow().AddDays(14));
            await signInManager.UserManager.UpdateAsync(user);
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        Response.Cookies.Delete("ExternalLoginState");

        return Redirect(validatedReturnUrl);
    }

    [HttpGet("exchange-ticket")]
    [AllowAnonymous]
    public async Task<IActionResult> ExchangeTicket([FromQuery] string ticket, [FromQuery] string? returnUrl)
    {
        LogExchangeTicketRequest(returnUrl);

        SignInTicketPayload? payload = ValidateSignInTicket(ticket);
        if (payload is null)
        {
            LogExchangeTicketInvalid();
            return BadRequest(new { succeeded = false, error = "invalid_or_expired_ticket" });
        }

        LogExchangeTicketValidated(payload.Email, payload.Jti);

        // Replay prevention: each ticket can only be exchanged once
        IDatabase redisDb = redis.GetDatabase();
        bool wasSet = await redisDb.StringSetAsync($"ticket:used:{payload.Jti}", "1", TimeSpan.FromSeconds(90), false, When.NotExists);
        if (!wasSet)
        {
            LogExchangeTicketAlreadyUsed(payload.Jti);
            return Unauthorized(new { succeeded = false, error = "ticket_already_used" });
        }

        WallowUser? user = await signInManager.UserManager.FindByEmailAsync(payload.Email);
        if (user is null)
        {
            LogExchangeTicketUserNotFound(payload.Email);
            return BadRequest(new { succeeded = false, error = "invalid_or_expired_ticket" });
        }

        await signInManager.SignInAsync(user, isPersistent: payload.RememberMe);
        LogExchangeTicketSignedIn(payload.Email, user.Id);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            LogExchangeTicketRedirecting(returnUrl);
            return Redirect(returnUrl);
        }

        string authUrl = GetRequiredAuthUrl();
        LogExchangeTicketNoReturnUrl(authUrl);
        return Redirect(authUrl);
    }

    [HttpGet("redirect-uri/validate")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateRedirectUri([FromQuery] string? uri, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return Ok(new { allowed = false });
        }

        bool result = await redirectUriValidator.IsAllowedAsync(uri, ct);
        return Ok(new { allowed = result });
    }

    [HttpPost("sign-out")]
    [Authorize]
    public async Task<IActionResult> SignOut([FromForm] string? postLogoutRedirectUri)
    {
        await signInManager.SignOutAsync();

        string authUrl = GetRequiredAuthUrl();

        if (!string.IsNullOrEmpty(postLogoutRedirectUri))
        {
            bool isAllowed = await redirectUriValidator.IsAllowedAsync(postLogoutRedirectUri);
            if (!isAllowed)
            {
                return Redirect($"{authUrl}/error?reason=invalid_redirect_uri");
            }
        }

        string redirectUrl = $"{authUrl}/logout?signed_out=true";
        if (!string.IsNullOrEmpty(postLogoutRedirectUri))
        {
            redirectUrl += $"&post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}";
        }

        return Redirect(redirectUrl);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] AccountRegisterRequest request)
    {
        bool isPasswordless = string.Equals(request.LoginMethod, "passwordless", StringComparison.OrdinalIgnoreCase);

        if (!isPasswordless && request.Password != request.ConfirmPassword)
        {
            return BadRequest(new { succeeded = false, error = "passwords_do_not_match" });
        }

        // Resolve tenant from client ID if provided
        ClientTenantInfo? tenantInfo = null;
        if (!string.IsNullOrEmpty(request.ClientId))
        {
            tenantInfo = await clientTenantResolver.ResolveAsync(request.ClientId);
            if (tenantInfo is null)
            {
                return BadRequest(new { succeeded = false, error = "invalid_client_id" });
            }
        }

        Guid tenantId = tenantInfo?.TenantId ?? Guid.Empty;

        // Self-registration uses placeholder names; users update their profile after onboarding
        WallowUser user = WallowUser.Create(
            tenantId: tenantId,
            firstName: "New",
            lastName: "User",
            email: request.Email,
            timeProvider: timeProvider);

        IdentityResult result;
        if (isPasswordless)
        {
            user.SetPasswordless();
            result = await signInManager.UserManager.CreateAsync(user);
        }
        else
        {
            result = await signInManager.UserManager.CreateAsync(user, request.Password);
        }

        if (!result.Succeeded)
        {
            string error = result.Errors.First().Code switch
            {
                "DuplicateEmail" or "DuplicateUserName" => "email_taken",
                _ => result.Errors.First().Description
            };
            return BadRequest(new { succeeded = false, error });
        }

        // Assign default "user" role so the token includes role claims for permission expansion
        IdentityResult roleResult = await signInManager.UserManager.AddToRoleAsync(user, "user");
        if (!roleResult.Succeeded)
        {
            LogRoleAssignmentFailed(user.Email!, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }

        // Add user as a member of the resolved organization
        if (tenantInfo is not null && tenantInfo.TenantId != Guid.Empty)
        {
            await organizationService.AddMemberAsync(tenantInfo.TenantId, user.Id);
        }

        string token = await signInManager.UserManager.GenerateEmailConfirmationTokenAsync(user);
        string authUrl = GetRequiredAuthUrl();
        string verifyUrl = $"{authUrl}/verify-email/confirm?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}";

        if (!string.IsNullOrEmpty(request.ReturnUrl) && await redirectUriValidator.IsAllowedAsync(request.ReturnUrl))
        {
            verifyUrl += $"&returnUrl={Uri.EscapeDataString(request.ReturnUrl)}";
        }

        LogEmailVerificationRequested(user.Email!);

        await messageBus.PublishAsync(new EmailVerificationRequestedEvent
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Email = user.Email!,
            FirstName = user.FirstName,
            VerifyUrl = verifyUrl
        });

        return Ok(new { succeeded = true });
    }

    [HttpGet("client-tenant/{clientId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetClientTenant(string clientId)
    {
        ClientTenantInfo? tenantInfo = await clientTenantResolver.ResolveAsync(clientId);
        if (tenantInfo is null)
        {
            return NotFound();
        }

        return Ok(new { tenantId = tenantInfo.TenantId, orgName = tenantInfo.TenantName });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] AccountForgotPasswordRequest request)
    {
        // Always return success to prevent email enumeration
        WallowUser? user = await signInManager.UserManager.FindByEmailAsync(request.Email);
        if (user is not null && await signInManager.UserManager.IsEmailConfirmedAsync(user))
        {
            string token = await signInManager.UserManager.GeneratePasswordResetTokenAsync(user);
            string authUrl = GetRequiredAuthUrl();
            string resetUrl = $"{authUrl}/reset-password?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}";

            LogPasswordResetRequested(user.Email!);

            await messageBus.PublishAsync(new PasswordResetRequestedEvent
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                Email = user.Email!,
                ResetToken = token,
                ResetUrl = resetUrl
            });
        }

        return Ok(new { succeeded = true });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] AccountResetPasswordRequest request)
    {
        WallowUser? user = await signInManager.UserManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return BadRequest(new { succeeded = false, error = "invalid_token" });
        }

        IdentityResult result = await signInManager.UserManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, error = "invalid_token" });
        }

        await messageBus.PublishAsync(new PasswordChangedEvent
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Email = user.Email!,
            FirstName = user.FirstName
        });

        return Ok(new { succeeded = true });
    }

    [HttpGet("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromQuery] string email, [FromQuery] string token)
    {
        WallowUser? user = await signInManager.UserManager.FindByEmailAsync(email);
        if (user is null)
        {
            return BadRequest(new { succeeded = false, error = "invalid_token" });
        }

        IdentityResult result = await signInManager.UserManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, error = "invalid_token" });
        }

        await messageBus.PublishAsync(new EmailVerifiedEvent
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName
        });

        return Ok(new { succeeded = true });
    }

    [HttpPost("passwordless/magic-link")]
    [AllowAnonymous]
    public async Task<IActionResult> SendMagicLink([FromBody] SendMagicLinkRequest request, CancellationToken ct)
    {
        PasswordlessResult result = await passwordlessService.SendMagicLinkAsync(request.Email, ct, request.ReturnUrl, request.ClientId);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, error = result.Error });
        }

        // Always return success to prevent email enumeration
        return Ok(new { succeeded = true });
    }

    [HttpGet("passwordless/magic-link/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyMagicLink([FromQuery] string token, [FromQuery] bool rememberMe = false, CancellationToken ct = default)
    {
        PasswordlessResult result = await passwordlessService.ValidateMagicLinkAsync(token, ct);
        if (!result.Succeeded)
        {
            return Unauthorized(new { succeeded = false, error = result.Error });
        }

        string signInTicket = CreateSignInTicket(result.Email!, rememberMe);
        return Ok(new { succeeded = true, email = result.Email, signInTicket });
    }

    [HttpPost("passwordless/otp")]
    [AllowAnonymous]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request, CancellationToken ct)
    {
        PasswordlessResult result = await passwordlessService.SendOtpAsync(request.Email, ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, error = result.Error });
        }

        // Always return success to prevent email enumeration
        return Ok(new { succeeded = true });
    }

    [HttpPost("passwordless/otp/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken ct)
    {
        PasswordlessResult result = await passwordlessService.ValidateOtpAsync(request.Email, request.Code, ct);
        if (!result.Succeeded)
        {
            return Unauthorized(new { succeeded = false, error = result.Error });
        }

        string signInTicket = CreateSignInTicket(result.Email!, request.RememberMe);
        return Ok(new { succeeded = true, email = result.Email, signInTicket });
    }

    [HttpPost("change-email")]
    [Authorize]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequest request)
    {
        string? userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { succeeded = false, error = "unauthorized" });
        }

        WallowUser? user = await signInManager.UserManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized(new { succeeded = false, error = "unauthorized" });
        }

        if (string.Equals(user.Email, request.NewEmail, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { succeeded = false, error = "same_email" });
        }

        // Rate limit: max 3 email change requests per hour per user
        IDatabase redisDb = redis.GetDatabase();
        string rateLimitKey = $"email:change:rate:{userId}";
        long count = await redisDb.StringIncrementAsync(rateLimitKey);
        if (count == 1)
        {
            await redisDb.KeyExpireAsync(rateLimitKey, TimeSpan.FromHours(1));
        }

        if (count > 3)
        {
            return StatusCode(429, new { succeeded = false, error = "rate_limited" });
        }

        DateTimeOffset expiry = timeProvider.GetUtcNow().AddHours(24);
        user.InitiateEmailChange(request.NewEmail, expiry, timeProvider);
        await signInManager.UserManager.UpdateAsync(user);

        string token = await signInManager.UserManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
        string authUrl = GetRequiredAuthUrl();
        string confirmUrl = $"{authUrl}/confirm-email-change?token={Uri.EscapeDataString(token)}&userId={Uri.EscapeDataString(userId)}&newEmail={Uri.EscapeDataString(request.NewEmail)}";

        await messageBus.PublishAsync(new UserEmailChangeRequestedEvent
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            NewEmail = request.NewEmail,
            ConfirmationUrl = confirmUrl,
            ExpiresAt = expiry
        });

        return Ok(new { succeeded = true });
    }

    [HttpGet("confirm-email-change")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmailChange(
        [FromQuery] string token,
        [FromQuery] string userId,
        [FromQuery] string newEmail)
    {
        WallowUser? user = await signInManager.UserManager.FindByIdAsync(userId);
        if (user is null)
        {
            return BadRequest(new { succeeded = false, error = "invalid_token" });
        }

        if (user.PendingEmailExpiry < timeProvider.GetUtcNow())
        {
            user.ClearPendingEmailChange();
            await signInManager.UserManager.UpdateAsync(user);
            return BadRequest(new { succeeded = false, error = "token_expired" });
        }

        string oldEmail = user.Email!;

        IdentityResult result = await signInManager.UserManager.ChangeEmailAsync(user, newEmail, token);
        if (!result.Succeeded)
        {
            return BadRequest(new { succeeded = false, error = "invalid_token" });
        }

        await signInManager.UserManager.SetUserNameAsync(user, newEmail);
        user.ClearPendingEmailChange();
        await signInManager.UserManager.UpdateAsync(user);

        await messageBus.PublishAsync(new UserEmailChangedEvent
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            OldEmail = oldEmail,
            NewEmail = newEmail
        });

        return Ok(new { succeeded = true });
    }

    private string GetRequiredAuthUrl() =>
        configuration["AuthUrl"] ?? throw new InvalidOperationException(
            "AuthUrl must be configured in appsettings.json. " +
            "Example: \"AuthUrl\": \"https://auth.yourdomain.com\"");

    private string CreateSignInTicket(string email, bool rememberMe)
    {
        ITimeLimitedDataProtector protector = dataProtectionProvider
            .CreateProtector(TicketPurpose)
            .ToTimeLimitedDataProtector();

        SignInTicketPayload payload = new(email, rememberMe, Guid.NewGuid());
        string json = JsonSerializer.Serialize(payload);
        return protector.Protect(json, _ticketLifetime);
    }

    private SignInTicketPayload? ValidateSignInTicket(string ticket)
    {
        try
        {
            ITimeLimitedDataProtector protector = dataProtectionProvider
                .CreateProtector(TicketPurpose)
                .ToTimeLimitedDataProtector();

            string json = protector.Unprotect(ticket);
            return JsonSerializer.Deserialize<SignInTicketPayload>(json);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
        {
            return null;
        }
    }

    private sealed record SignInTicketPayload(string Email, bool RememberMe, Guid Jti);

    [LoggerMessage(Level = LogLevel.Information, Message = "Email verification requested for {Email}")]
    private partial void LogEmailVerificationRequested(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset requested for {Email}")]
    private partial void LogPasswordResetRequested(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "External login failed for provider {Provider}: {Reason}")]
    private partial void LogExternalLoginFailed(string provider, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to assign default role to user {Email}: {Errors}")]
    private partial void LogRoleAssignmentFailed(string email, string errors);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC login attempt for {Email}")]
    private partial void LogLoginAttempt(string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC login user not found: {Email}")]
    private partial void LogLoginUserNotFound(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC login password valid for {Email}")]
    private partial void LogLoginPasswordValid(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC login MFA required for {Email}")]
    private partial void LogLoginMfaRequired(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC login MFA grace period active for {Email}")]
    private partial void LogLoginMfaGracePeriod(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC login MFA enrollment required (grace expired) for {Email}")]
    private partial void LogLoginMfaEnrollmentRequired(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC login succeeded, ticket issued for {Email}")]
    private partial void LogLoginSucceededWithTicket(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC exchange-ticket request: returnUrl={ReturnUrl}")]
    private partial void LogExchangeTicketRequest(string? returnUrl);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC exchange-ticket: ticket validation failed (invalid or expired)")]
    private partial void LogExchangeTicketInvalid();

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC exchange-ticket validated: email={Email}, jti={Jti}")]
    private partial void LogExchangeTicketValidated(string email, Guid jti);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC exchange-ticket already used: jti={Jti}")]
    private partial void LogExchangeTicketAlreadyUsed(Guid jti);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OIDC exchange-ticket user not found: {Email}")]
    private partial void LogExchangeTicketUserNotFound(string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC exchange-ticket signed in: email={Email}, userId={UserId}")]
    private partial void LogExchangeTicketSignedIn(string email, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC exchange-ticket redirecting to returnUrl={ReturnUrl}")]
    private partial void LogExchangeTicketRedirecting(string returnUrl);

    [LoggerMessage(Level = LogLevel.Information, Message = "OIDC exchange-ticket no valid returnUrl, redirecting to authUrl={AuthUrl}")]
    private partial void LogExchangeTicketNoReturnUrl(string authUrl);
}
