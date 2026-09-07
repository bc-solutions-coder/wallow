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
using Wallow.Identity.Api.Contracts.Responses;
using Wallow.Identity.Application.DTOs;
using Wallow.Identity.Application.Helpers;
using Wallow.Identity.Application.Interfaces;
using Wallow.Identity.Domain.Entities;
using Wallow.Identity.Domain.Errors;
using Wallow.Shared.Api.Extensions;
using Wallow.Shared.Api.Problems;
using Wallow.Shared.Contracts.Identity.Events;
using Wallow.Shared.Kernel.Errors;
using Wallow.Shared.Kernel.Extensions;
using Wallow.Shared.Kernel.Results;
using Wolverine;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// Browser sign-in, registration, password recovery, and account-verification endpoints.
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
    IPasswordlessService passwordlessService,
    IMfaExemptionChecker mfaExemptionChecker,
    IMfaService mfaService,
    IMfaPartialAuthService mfaPartialAuthService,
    IOrganizationMfaPolicyService orgMfaPolicyService,
    IMfaLockoutService mfaLockoutService,
    IConnectionMultiplexer redis,
    ILogger<AccountController> logger,
    TimeProvider timeProvider,
    IEmailChangeRateLimiter emailChangeRateLimiter) : ControllerBase
{
    private const string TicketPurpose = "SignInTicket";

    /// <summary>
    /// Authentication-properties key carrying the requesting client through external sign-in.
    /// </summary>
    private const string ExternalLoginClientIdKey = "client_id";

    private static readonly TimeSpan _ticketLifetime = TimeSpan.FromSeconds(60);

    [HttpGet("external-providers")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExternalProviders()
    {
        IEnumerable<AuthenticationScheme> schemes = await signInManager.GetExternalAuthenticationSchemesAsync();
        // ExternalLogin looks up providers by scheme name.
        List<string> providers = schemes
            .Select(s => s.Name)
            .ToList();
        return Ok(providers);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccountLoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] AccountLoginRequest request, CancellationToken ct)
    {
        LogLoginAttempt(request.Email);
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        WallowUser? user = await signInManager.UserManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            LogLoginUserNotFound(request.Email);

            // Authentication audit events have no organization until token issuance selects one.
            await messageBus.PublishAsync(new UserLoginFailedEvent
            {
                UserId = Guid.Empty,
                IpAddress = ipAddress,
                Reason = "account_not_found"
            });
            return this.Problem(IdentityErrors.AuthInvalidCredentials);
        }

        Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.CheckPasswordSignInAsync(
            user, request.Password, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            LogLoginPasswordValid(user.Email!);


            if (user.MfaEnabled && !await mfaExemptionChecker.IsExemptAsync(user, ct))
            {
                LogLoginMfaRequired(user.Email!);
                await mfaPartialAuthService.IssuePartialCookieAsync(
                    new MfaPartialAuthPayload(user.Id.ToString(), user.Email!, "password", request.RememberMe, timeProvider.GetUtcNow()),
                    ct);
                return Ok(new { succeeded = false, mfaRequired = true });
            }


            OrgMfaPolicyResult? orgPolicy = await orgMfaPolicyService.CheckAsync(user.Id, ct);
            if (orgPolicy is { RequiresMfa: true })
            {
                if (orgPolicy.IsInGracePeriod)
                {
                    LogLoginMfaGracePeriod(user.Email!);
                    // Grace permits full sign-in while enrollment remains required.
                    await messageBus.PublishAsync(new UserLoginSucceededEvent
                    {
                        UserId = user.Id,
                        IpAddress = ipAddress
                    });
                    string graceTicket = CreateSignInTicket(user.Email!, request.RememberMe);
                    return Ok(new { succeeded = true, mfaEnrollmentRequired = true, mfaGraceDeadline = user.MfaGraceDeadline, signInTicket = graceTicket });
                }

                LogLoginMfaEnrollmentRequired(user.Email!);

                await mfaPartialAuthService.IssuePartialCookieAsync(
                    new MfaPartialAuthPayload(user.Id.ToString(), user.Email!, "password", request.RememberMe, timeProvider.GetUtcNow()),
                    ct);
                return Ok(new { succeeded = false, mfaEnrollmentRequired = true });
            }


            await messageBus.PublishAsync(new UserLoginSucceededEvent
            {
                UserId = user.Id,
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
                IpAddress = ipAddress
            });
            return this.Problem(IdentityErrors.AuthLockedOut);
        }

        if (result.IsNotAllowed)
        {
            return this.Problem(IdentityErrors.AuthEmailNotConfirmed);
        }

        await messageBus.PublishAsync(new UserLoginFailedEvent
        {
            UserId = user.Id,
            IpAddress = ipAddress,
            Reason = "invalid_credentials"
        });
        return this.Problem(IdentityErrors.AuthInvalidCredentials);
    }

    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MfaChallengeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyMfaChallenge([FromBody] Contracts.Requests.MfaVerifyRequest request, CancellationToken ct)
    {
        string? mfaIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        MfaPartialAuthPayload? payload = await mfaPartialAuthService.ValidatePartialCookieAsync(ct);
        if (payload is null)
        {
            return this.Problem(IdentityErrors.MfaSessionMissing);
        }

        WallowUser? user = await signInManager.UserManager.FindByIdAsync(payload.UserId);
        if (user is null || string.IsNullOrEmpty(user.TotpSecretEncrypted))
        {
            return this.Problem(IdentityErrors.MfaCodeInvalid);
        }


        if (user.IsMfaLockedOut(timeProvider))
        {
            await messageBus.PublishAsync(new UserMfaLockedOutEvent
            {
                UserId = user.Id,
                LockoutCount = user.MfaLockoutCount
            });
            return this.Problem(IdentityErrors.MfaLockedOut);
        }


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
                    LockoutCount = lockoutResult.LockoutCount
                });
                return this.Problem(IdentityErrors.MfaLockedOut);
            }

            await messageBus.PublishAsync(new UserLoginFailedEvent
            {
                UserId = user.Id,
                IpAddress = mfaIpAddress,
                Reason = "invalid_mfa_code"
            });
            return this.Problem(IdentityErrors.MfaCodeInvalid);
        }

        await mfaLockoutService.ResetAsync(user.Id, ct);
        await mfaPartialAuthService.UpgradeToFullAuthAsync(payload.UserId, payload.RememberMe, ct);

        await messageBus.PublishAsync(new UserLoginSucceededEvent
        {
            UserId = user.Id,
            IpAddress = mfaIpAddress
        });

        string? email = user.Email;
        string? signInTicket = email is not null ? CreateSignInTicket(email, payload.RememberMe) : null;
        return Ok(new { succeeded = true, signInTicket });
    }

    [HttpGet("external-login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> ExternalLogin(
        [FromQuery] string provider,
        [FromQuery] string returnUrl,
        [FromQuery] string? clientId = null)
    {
        if (string.IsNullOrEmpty(provider))
        {
            return this.Problem(IdentityErrors.AuthProviderRequired);
        }

        AuthenticationScheme? scheme = await authSchemeProvider.GetSchemeAsync(provider);
        if (scheme is null)
        {
            return this.Problem(IdentityErrors.AuthProviderUnsupported);
        }

        string authUrl = GetRequiredAuthUrl();

        if (string.IsNullOrEmpty(returnUrl) || !await redirectUriValidator.IsAllowedAsync(returnUrl, clientId))
        {
            return Redirect($"{authUrl}/error?reason=invalid_redirect_uri");
        }

        string callbackUrl = Url.Action(nameof(ExternalLoginCallback), new { returnUrl })!;
        // Allow a custom SignInManager to return no authentication properties.
        AuthenticationProperties properties =
            signInManager.ConfigureExternalAuthenticationProperties(provider, callbackUrl)
            ?? new AuthenticationProperties { RedirectUri = callbackUrl };

        // Carry client context in authentication properties across the external challenge.
        if (!string.IsNullOrEmpty(clientId))
        {
            properties.Items[ExternalLoginClientIdKey] = clientId;
        }

        return Challenge(properties, provider);
    }

    [HttpGet("external-login-callback")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> ExternalLoginCallback(
        [FromQuery] string returnUrl,
        [FromQuery] string? clientId = null)
    {
        string authUrl = GetRequiredAuthUrl();

        // Read the stashed client id before applying its redirect-origin allow-list.
        ExternalLoginInfo? info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            LogExternalLoginFailed("unknown", "No external login info available");
            return Redirect($"{authUrl}/login?error=external_login_failed");
        }


        string? flowClientId = !string.IsNullOrEmpty(clientId)
            ? clientId
            : GetStashedClientId(info.AuthenticationProperties);
        string clientIdQuery = BuildClientIdQuery(flowClientId);


        if (string.IsNullOrEmpty(returnUrl) || !await redirectUriValidator.IsAllowedAsync(returnUrl, flowClientId))
        {
            returnUrl = authUrl;
        }

        // Attempt sign-in through an existing provider link.
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
                return Redirect($"{authUrl}/mfa/challenge?returnUrl={encodedReturn}{clientIdQuery}");
            }


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
                        return Redirect($"{authUrl}/mfa/challenge?returnUrl={encodedReturn}{clientIdQuery}");
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

        // Link an existing account only when the provider reports a verified email.
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

        // Defer account creation until terms are accepted.
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
        return Redirect($"{authUrl}/accept-terms?returnUrl={encodedReturnUrl}&email={encodedEmail}&name={encodedName}{clientIdQuery}");
    }

    [HttpGet("complete-external-registration")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> CompleteExternalRegistration(
        [FromQuery] bool acceptedTerms,
        [FromQuery] string returnUrl,
        [FromQuery] string? clientId = null)
    {
        string authUrl = GetRequiredAuthUrl();


        string validatedReturnUrl = authUrl;
        if (!string.IsNullOrEmpty(returnUrl) && await redirectUriValidator.IsAllowedAsync(returnUrl, clientId))
        {
            validatedReturnUrl = returnUrl;
        }

        if (!acceptedTerms)
        {
            return Redirect(
                $"{authUrl}/accept-terms?error=terms_required&returnUrl={Uri.EscapeDataString(validatedReturnUrl)}{BuildClientIdQuery(clientId)}");
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
            // Accept both byte-protected and string-protected cookie formats.
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

        // The account may have been created while the terms page was open.
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
                Email = user.Email!,
                FirstName = user.FirstName,
                VerifyUrl = verifyUrl
            });
        }


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
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> ExchangeTicket(
        [FromQuery] string ticket,
        [FromQuery] string? returnUrl,
        [FromQuery] string? clientId = null)
    {
        LogExchangeTicketRequest(returnUrl);

        SignInTicketPayload? payload = ValidateSignInTicket(ticket);
        if (payload is null)
        {
            LogExchangeTicketInvalid();
            return this.Problem(IdentityErrors.AuthTicketInvalid);
        }

        LogExchangeTicketValidated(payload.Email, payload.Jti);

        // Atomically reserve the ticket id in Redis for longer than the ticket lifetime.
        IDatabase redisDb = redis.GetDatabase();
        bool wasSet = await redisDb.StringSetAsync($"ticket:used:{payload.Jti}", "1", TimeSpan.FromSeconds(90), false, When.NotExists);
        if (!wasSet)
        {
            LogExchangeTicketAlreadyUsed(payload.Jti);
            return this.Problem(IdentityErrors.AuthTicketAlreadyUsed);
        }

        WallowUser? user = await signInManager.UserManager.FindByEmailAsync(payload.Email);
        if (user is null)
        {
            LogExchangeTicketUserNotFound(payload.Email);
            return this.Problem(IdentityErrors.AuthTicketInvalid);
        }

        await signInManager.SignInAsync(user, isPersistent: payload.RememberMe);
        LogExchangeTicketSignedIn(payload.Email, user.Id);

        // Permit local routes and allowed absolute origins; otherwise return to AuthUrl.
        if (!string.IsNullOrEmpty(returnUrl)
            && (Url.IsLocalUrl(returnUrl) || await redirectUriValidator.IsAllowedAsync(returnUrl, clientId)))
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
    [ProducesResponseType(typeof(RedirectUriValidationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateRedirectUri(
        [FromQuery] string? uri,
        [FromQuery] string? clientId,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(uri))
        {
            return Ok(new { allowed = false });
        }

        bool result = await redirectUriValidator.IsAllowedAsync(uri, clientId, ct);
        return Ok(new { allowed = result });
    }

    [HttpPost("sign-out")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> SignOut(
        [FromForm] string? postLogoutRedirectUri,
        [FromForm] string? clientId = null)
    {
        await signInManager.SignOutAsync();

        string authUrl = GetRequiredAuthUrl();

        if (!string.IsNullOrEmpty(postLogoutRedirectUri))
        {
            bool isAllowed = await redirectUriValidator.IsAllowedAsync(postLogoutRedirectUri, clientId);
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
    [ProducesResponseType(typeof(AccountOperationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Register([FromBody] AccountRegisterRequest request)
    {
        bool isPasswordless = string.Equals(request.LoginMethod, "passwordless", StringComparison.OrdinalIgnoreCase);

        if (!isPasswordless && request.Password != request.ConfirmPassword)
        {
            return this.Problem(IdentityErrors.AuthPasswordsDoNotMatch);
        }

        // Supplied client ids must resolve to an organization.
        if (!string.IsNullOrEmpty(request.ClientId)
            && await clientTenantResolver.ResolveAsync(request.ClientId) is null)
        {
            return this.Problem(IdentityErrors.AuthClientIdInvalid);
        }

        // Names are placeholders until the user edits their profile.
        WallowUser user = WallowUser.Create(
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
            IdentityError failure = result.Errors.First();
            return failure.Code switch
            {
                "DuplicateEmail" or "DuplicateUserName" => this.Problem(IdentityErrors.AuthEmailTaken),
                _ => IdentityValidationProblem(failure)
            };
        }

        // Enrollment during authorization decides membership; anonymous registration grants none.

        string token = await signInManager.UserManager.GenerateEmailConfirmationTokenAsync(user);
        string authUrl = GetRequiredAuthUrl();
        string verifyUrl = $"{authUrl}/verify-email/confirm?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email!)}";

        // Preserve local invitation routes as well as allowed absolute return URLs.
        if (!string.IsNullOrEmpty(request.ReturnUrl)
            && (Url.IsLocalUrl(request.ReturnUrl)
                || await redirectUriValidator.IsAllowedAsync(request.ReturnUrl, request.ClientId)))
        {
            verifyUrl += $"&returnUrl={Uri.EscapeDataString(request.ReturnUrl)}";
        }

        LogEmailVerificationRequested(user.Email!);

        await messageBus.PublishAsync(new EmailVerificationRequestedEvent
        {
            UserId = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            VerifyUrl = verifyUrl
        });

        return Ok(new { succeeded = true });
    }

    [HttpGet("client-tenant/{clientId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ClientTenantResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClientTenant(string clientId)
    {
        ClientTenantInfo? tenantInfo = await clientTenantResolver.ResolveAsync(clientId);
        if (tenantInfo is null)
        {
            return this.Problem(SharedErrors.NotFound);
        }

        return Ok(new { tenantId = tenantInfo.TenantId, orgName = tenantInfo.TenantName });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccountOperationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] AccountForgotPasswordRequest request)
    {
        // Unknown and unconfirmed emails receive the same success response.
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
                Email = user.Email!,
                ResetToken = token,
                ResetUrl = resetUrl
            });
        }

        return Ok(new { succeeded = true });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccountOperationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword([FromBody] AccountResetPasswordRequest request)
    {
        WallowUser? user = await signInManager.UserManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return this.Problem(IdentityErrors.AuthTokenInvalid);
        }

        // Keep password-policy failures distinct from invalid reset links.
        IdentityResult result = await signInManager.UserManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            IdentityError failure = result.Errors.First();
            return failure.Code == "InvalidToken"
                ? this.Problem(IdentityErrors.AuthTokenInvalid)
                : IdentityValidationProblem(failure);
        }

        await messageBus.PublishAsync(new PasswordChangedEvent
        {
            UserId = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName
        });

        return Ok(new { succeeded = true });
    }

    [HttpGet("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccountOperationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyEmail([FromQuery] string email, [FromQuery] string token)
    {
        WallowUser? user = await signInManager.UserManager.FindByEmailAsync(email);
        if (user is null)
        {
            return this.Problem(IdentityErrors.AuthTokenInvalid);
        }

        IdentityResult result = await signInManager.UserManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return this.Problem(IdentityErrors.AuthTokenInvalid);
        }

        await messageBus.PublishAsync(new EmailVerifiedEvent
        {
            UserId = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName
        });

        return Ok(new { succeeded = true });
    }

    [HttpPost("passwordless/magic-link")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccountOperationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendMagicLink([FromBody] SendMagicLinkRequest request, CancellationToken ct)
    {
        Result result = await passwordlessService.SendMagicLinkAsync(request.Email, ct, request.ReturnUrl, request.ClientId);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        // A successful dispatch result does not disclose whether the email exists.
        return Ok(new { succeeded = true });
    }

    [HttpGet("passwordless/magic-link/verify")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PasswordlessVerificationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyMagicLink([FromQuery] string token, [FromQuery] bool rememberMe = false, CancellationToken ct = default)
    {
        Result<string> result = await passwordlessService.ValidateMagicLinkAsync(token, ct);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        string email = result.Value;
        string signInTicket = CreateSignInTicket(email, rememberMe);
        return Ok(new { succeeded = true, email, signInTicket });
    }

    [HttpPost("passwordless/otp")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccountOperationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request, CancellationToken ct)
    {
        Result result = await passwordlessService.SendOtpAsync(request.Email, ct);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        // A successful dispatch result does not disclose whether the email exists.
        return Ok(new { succeeded = true });
    }

    [HttpPost("passwordless/otp/verify")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PasswordlessVerificationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken ct)
    {
        Result<string> result = await passwordlessService.ValidateOtpAsync(request.Email, request.Code, ct);
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }

        string email = result.Value;
        string signInTicket = CreateSignInTicket(email, request.RememberMe);
        return Ok(new { succeeded = true, email, signInTicket });
    }

    [HttpPost("change-email")]
    [Authorize]
    [ProducesResponseType(typeof(AccountOperationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequest request)
    {
        string? userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        WallowUser? user = await signInManager.UserManager.FindByIdAsync(userId);
        if (user is null)
        {
            return this.Problem(SharedErrors.Unauthenticated);
        }

        if (string.Equals(user.Email, request.NewEmail, StringComparison.OrdinalIgnoreCase))
        {
            return this.Problem(IdentityErrors.AuthEmailUnchanged);
        }

        Result throttle = await emailChangeRateLimiter.CheckAsync(userId);
        if (throttle.IsFailure)
        {
            return this.Problem(SharedErrors.RateLimitExceeded, retryAfter: throttle.Error.RetryAfter);
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
            NewEmail = request.NewEmail,
            ConfirmationUrl = confirmUrl,
            ExpiresAt = expiry
        });

        return Ok(new { succeeded = true });
    }

    [HttpGet("confirm-email-change")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccountOperationResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmEmailChange(
        [FromQuery] string token,
        [FromQuery] string userId,
        [FromQuery] string newEmail)
    {
        WallowUser? user = await signInManager.UserManager.FindByIdAsync(userId);
        if (user is null)
        {
            return this.Problem(IdentityErrors.AuthTokenInvalid);
        }

        if (user.PendingEmailExpiry < timeProvider.GetUtcNow())
        {
            user.ClearPendingEmailChange();
            await signInManager.UserManager.UpdateAsync(user);
            return this.Problem(IdentityErrors.AuthTokenExpired);
        }

        string oldEmail = user.Email!;

        IdentityResult result = await signInManager.UserManager.ChangeEmailAsync(user, newEmail, token);
        if (!result.Succeeded)
        {
            return this.Problem(IdentityErrors.AuthTokenInvalid);
        }

        await signInManager.UserManager.SetUserNameAsync(user, newEmail);
        user.ClearPendingEmailChange();
        await signInManager.UserManager.UpdateAsync(user);

        await messageBus.PublishAsync(new UserEmailChangedEvent
        {
            UserId = user.Id,
            OldEmail = oldEmail,
            NewEmail = newEmail
        });

        return Ok(new { succeeded = true });
    }

    /// <summary>
    /// Reads the requesting client from external authentication properties.
    /// </summary>
    private static string? GetStashedClientId(AuthenticationProperties? properties) =>
        properties is not null && properties.Items.TryGetValue(ExternalLoginClientIdKey, out string? stashed)
            ? stashed
            : null;

    /// <summary>
    /// Appends an escaped client_id query parameter, or nothing when the client id is empty.
    /// </summary>
    private static string BuildClientIdQuery(string? clientId) =>
        string.IsNullOrEmpty(clientId)
            ? string.Empty
            : $"&{ExternalLoginClientIdKey}={Uri.EscapeDataString(clientId)}";

    /// <summary>
    /// Returns Validation.Failed, exposing Identity descriptions only for password-policy errors.
    /// </summary>
    private ProblemResult IdentityValidationProblem(IdentityError failure) =>
        failure.Code.StartsWith("Password", StringComparison.Ordinal)
            ? this.Problem(SharedErrors.ValidationFailed, failure.Description)
            : this.Problem(SharedErrors.ValidationFailed);

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
