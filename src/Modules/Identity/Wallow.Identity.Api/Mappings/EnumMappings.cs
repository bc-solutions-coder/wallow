using Wallow.Identity.Api.Contracts.Enums;
using Wallow.Identity.Domain.Enums;

namespace Wallow.Identity.Api.Mappings;

/// <summary>Extension methods for mapping between API and Domain enums.</summary>
public static class EnumMappings
{
    public static SamlNameIdFormat ToDomain(this ApiSamlNameIdFormat api) => api switch
    {
        ApiSamlNameIdFormat.Email => SamlNameIdFormat.Email,
        ApiSamlNameIdFormat.Persistent => SamlNameIdFormat.Persistent,
        ApiSamlNameIdFormat.Transient => SamlNameIdFormat.Transient,
        ApiSamlNameIdFormat.Unspecified => SamlNameIdFormat.Unspecified,
        _ => throw new ArgumentOutOfRangeException(nameof(api), api, "Unknown SAML NameID format")
    };

    public static ApiSamlNameIdFormat ToApi(this SamlNameIdFormat domain) => domain switch
    {
        SamlNameIdFormat.Email => ApiSamlNameIdFormat.Email,
        SamlNameIdFormat.Persistent => ApiSamlNameIdFormat.Persistent,
        SamlNameIdFormat.Transient => ApiSamlNameIdFormat.Transient,
        SamlNameIdFormat.Unspecified => ApiSamlNameIdFormat.Unspecified,
        _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unknown SAML NameID format")
    };
}
