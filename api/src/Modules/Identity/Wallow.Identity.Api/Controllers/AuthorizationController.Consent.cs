using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// The consent half of the authorize endpoint: what the consent screen is sent, and how the
/// decision it posts back is tied to the request it answers.
/// </summary>
public sealed partial class AuthorizationController
{
    /// <summary>The form field the consent screen posts its single-use token under.</summary>
    public const string ConsentTokenParameter = "consent_token";

    /// <summary>The form field carrying the decision: <see cref="ConsentGranted"/> or <see cref="ConsentDenied"/>.</summary>
    public const string ConsentDecisionParameter = "consent_decision";

    public const string ConsentGranted = "granted";
    public const string ConsentDenied = "denied";

    /// <summary>
    /// The parameters that carry a consent decision rather than describe the authorize request.
    /// They are stripped from the request the consent screen is told to come back to, and from the
    /// fingerprint the token is bound to, so the same request digests the same way on the GET that
    /// mints the token and the POST that redeems it. The two retired GET flags are listed so a link
    /// still carrying them is not treated as a different request.
    /// </summary>
    private static readonly ImmutableHashSet<string> _consentParameters = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        ConsentTokenParameter,
        ConsentDecisionParameter,
        "consent_granted",
        "consent_denied");

    /// <summary>
    /// Sends the user to the consent screen with the request to come back to, the client and
    /// scopes the decision is about, and a token minted for this user and this request.
    /// </summary>
    private RedirectResult RedirectToConsent(
        OpenIddictRequest request,
        string userId,
        string? clientId,
        ImmutableArray<string> grantedScopes,
        string fingerprint)
    {
        string authUrl = GetRequiredAuthUrl();

        // Rebuilt from the OpenIddict request rather than read off the URL: a POSTed decision
        // carries the request in its body, and the GET may carry flags that must not come back.
        string returnUrl = Request.PathBase + Request.Path + QueryString.Create(AuthorizeParameters(request));

        // The granted scopes ride along space-delimited (OAuth's own delimiter, and what the
        // consent-info endpoint splits on): they are the substance of the decision the screen asks
        // the user to make, and asking to consent to a scope that will never be issued is a lie.
        string consentScopes = string.Join(" ", grantedScopes);
        string token = consentTokenService.Issue(userId, fingerprint);

        LogRedirectingToConsent(clientId, returnUrl);
        return Redirect($"{authUrl}/consent?returnUrl={Uri.EscapeDataString(returnUrl)}" +
            $"&client_id={Uri.EscapeDataString(clientId ?? string.Empty)}" +
            $"&scope={Uri.EscapeDataString(consentScopes)}" +
            $"&{ConsentTokenParameter}={Uri.EscapeDataString(token)}");
    }

    /// <summary>
    /// A digest of the authorize request a consent decision answers, stable across the GET that
    /// shows the screen and the POST that answers it.
    /// </summary>
    private static string ConsentRequestFingerprint(OpenIddictRequest request)
    {
        StringBuilder canonical = new();
        foreach ((string name, string? value) in AuthorizeParameters(request))
        {
            canonical.Append(name).Append('=').Append(value).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    /// <summary>The request's parameters minus the consent ones, in one fixed order.</summary>
    private static IEnumerable<KeyValuePair<string, string?>> AuthorizeParameters(OpenIddictRequest request) =>
        request.GetParameters()
            .Where(parameter => !_consentParameters.Contains(parameter.Key))
            .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
            .Select(parameter => new KeyValuePair<string, string?>(parameter.Key, (string?)parameter.Value));
}
