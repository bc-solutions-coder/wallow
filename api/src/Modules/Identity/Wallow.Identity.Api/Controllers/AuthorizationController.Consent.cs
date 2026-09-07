using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace Wallow.Identity.Api.Controllers;

/// <summary>
/// Consent-screen redirects and request fingerprints for posted consent decisions.
/// </summary>
public sealed partial class AuthorizationController
{
    /// <summary>
    /// Form field carrying the consent token.
    /// </summary>
    public const string ConsentTokenParameter = "consent_token";

    /// <summary>The form field carrying the decision: <see cref="ConsentGranted"/> or <see cref="ConsentDenied"/>.</summary>
    public const string ConsentDecisionParameter = "consent_decision";

    public const string ConsentGranted = "granted";
    public const string ConsentDenied = "denied";

    /// <summary>
    /// Consent fields excluded from both the return URL and request fingerprint.
    /// </summary>
    private static readonly ImmutableHashSet<string> _consentParameters = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        ConsentTokenParameter,
        ConsentDecisionParameter);

    /// <summary>
    /// Builds a consent redirect with the user-bound token and scopes to display.
    /// </summary>
    private RedirectResult RedirectToConsent(
        OpenIddictRequest request,
        string userId,
        string? clientId,
        ImmutableArray<string> grantedScopes,
        string fingerprint)
    {
        string authUrl = GetRequiredAuthUrl();

        // Rebuild from parsed parameters so POST bodies retain the authorize request.
        string returnUrl = Request.PathBase + Request.Path + QueryString.Create(AuthorizeParameters(request));

        // The consent screen and authorize-context endpoint use space-delimited scopes.
        string consentScopes = string.Join(" ", grantedScopes);
        string token = consentTokenService.Issue(userId, fingerprint);

        LogRedirectingToConsent(clientId, returnUrl);
        return Redirect($"{authUrl}/consent?returnUrl={Uri.EscapeDataString(returnUrl)}" +
            $"&client_id={Uri.EscapeDataString(clientId ?? string.Empty)}" +
            $"&scope={Uri.EscapeDataString(consentScopes)}" +
            $"&{ConsentTokenParameter}={Uri.EscapeDataString(token)}");
    }

    /// <summary>
    /// Hashes sorted authorize parameters after removing consent fields.
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
